using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using TimeSheet.Domain;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.ExternalServices;

/// <summary>
/// Calls the Anthropic Messages API directly over HTTP (no SDK dependency) using strict tool-use: the model is
/// forced (tool_choice) to call a single tool whose input_schema matches ProjectHealthVerdict, so the response
/// is parsed straight from validated JSON - never regex/free-text parsing. Sonnet (not Opus) since this runs
/// nightly across every active project - a routine classification+short-explanation task.
/// </summary>
public class ClaudeHealthAnalysisClient(HttpClient httpClient, IConfiguration configuration) : IHealthAnalysisClient
{
    private const string ToolName = "record_project_health_verdict";

    public async Task<ProjectHealthVerdict> AnalyzeAsync(ProjectHealthAnalysisInput input, CancellationToken ct)
    {
        var apiKey = configuration["Claude:ApiKey"] ?? throw new InvalidOperationException("Missing Claude:ApiKey configuration.");
        var model = configuration["Claude:Model"] ?? "claude-sonnet-5";

        var requestBody = new
        {
            model,
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = BuildPrompt(input) } },
            tools = new[]
            {
                new
                {
                    name = ToolName,
                    description = "Record the project health assessment verdict.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            status = new { type = "string", @enum = new[] { "OnTrack", "AtRisk", "Behind" } },
                            summary = new { type = "string", description = "1-3 sentence, admin-facing plain-language explanation." },
                            contributingFactors = new { type = "array", items = new { type = "string" } },
                            recommendedAction = new { type = "string" },
                        },
                        required = new[] { "status", "summary", "contributingFactors" },
                    },
                },
            },
            tool_choice = new { type = "tool", name = ToolName },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(requestBody);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = JsonNode.Parse(await response.Content.ReadAsStreamAsync(ct))
            ?? throw new InvalidOperationException("Empty response from Claude API.");

        var toolUseBlock = responseJson["content"]?.AsArray()
            .FirstOrDefault(block => block?["type"]?.GetValue<string>() == "tool_use")
            ?? throw new InvalidOperationException("Claude API response did not contain a tool_use block.");

        var toolInput = toolUseBlock["input"] ?? throw new InvalidOperationException("tool_use block had no input.");

        var status = Enum.Parse<ProjectHealthStatus>(toolInput["status"]!.GetValue<string>());
        var summary = toolInput["summary"]!.GetValue<string>();
        var contributingFactors = toolInput["contributingFactors"]?.AsArray().Select(f => f!.GetValue<string>()).ToList() ?? [];
        var recommendedAction = toolInput["recommendedAction"]?.GetValue<string>();

        return new ProjectHealthVerdict(status, summary, contributingFactors, recommendedAction);
    }

    private static string BuildPrompt(ProjectHealthAnalysisInput input)
    {
        var recentWork = input.RecentEntryDescriptions.Count == 0
            ? "(no recent timesheet descriptions available)"
            : string.Join("\n", input.RecentEntryDescriptions.Select(d => $"- {d}"));

        return $"""
            You are assessing whether a consultancy project is on track, at risk, or behind schedule/budget.

            Project: {input.ProjectName} (client: {input.ClientName})
            Payment model: {input.PaymentModel}
            Budget: {input.BudgetHours?.ToString("0.0") ?? "n/a"} hours{(input.FixedFeeAmount is not null ? $", fixed fee {input.FixedFeeAmount:0.00}" : "")}
            Start date: {input.StartDate:yyyy-MM-dd}, planned end: {input.PlannedEndDate?.ToString("yyyy-MM-dd") ?? "open-ended"}
            Total hours logged to date: {input.TotalHoursLoggedToDate:0.0}
            Hours logged in the last 7 days: {input.HoursLoggedLast7Days:0.0} (previous 7 days: {input.HoursLoggedPrevious7Days:0.0})
            Hours logged in the last 30 days: {input.HoursLoggedLast30Days:0.0}
            Percent of budget consumed: {input.PercentBudgetConsumed?.ToString("0.0") ?? "n/a"}%
            Percent of planned timeline elapsed: {input.PercentTimeElapsed?.ToString("0.0") ?? "n/a"}%

            Recent timesheet entry descriptions (most recent first):
            {recentWork}

            Based on this, record your verdict using the record_project_health_verdict tool.
            """;
    }
}
