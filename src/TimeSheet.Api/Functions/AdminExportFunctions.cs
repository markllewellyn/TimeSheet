using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>CSV export of timesheet entries - FDD: "Entries can be exported to CSV. The export is available
/// from the settings page for both users and administrators. Data can be exported on a client, project or
/// project manager basis, for all users or for specific users." A non-admin can only ever export their own
/// entries (optionally narrowed further by project) - clientId/userId/projectManagerUserId are silently
/// ignored for a non-admin caller, enforced here rather than only hidden in the UI. Date-range presets (last 7
/// days / last calendar month / custom) are a frontend-only concern - the API just takes from/to.</summary>
public class AdminExportFunctions(ITimesheetEntryRepository entries, IProjectRepository projects, ICurrentUserAccessor currentUser)
{
    [Function("Admin_ExportTimesheetEntries")]
    public async Task<IActionResult> ExportTimesheetEntries(
        // Deliberately not "admin/..." - Azure Functions reserves that path prefix for its own host API and
        // silently drops user-defined routes that start with it (same constraint already noted on
        // Users_ResetPassword in UsersFunctions.cs).
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "exports/timesheet-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();

        var from = ParseDate(req, "from");
        var to = ParseDate(req, "to");
        var projectId = ParseInt(req, "projectId");

        int? clientId = null;
        int? userId = user.UserId;
        IReadOnlyCollection<int>? projectIds = null;

        if (user.IsAdmin)
        {
            clientId = ParseInt(req, "clientId");
            userId = ParseInt(req, "userId"); // null (not this admin's own id) means "all users"

            if (ParseInt(req, "projectManagerUserId") is { } pmUserId)
            {
                var managed = await projects.GetManagedByUserAsync(pmUserId, ct);
                projectIds = managed.Select(p => p.Id).ToList();
            }
        }

        var rows = await entries.GetAllInRangeAsync(from, to, clientId, projectId, projectIds, userId, ct);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', [
            "Staff Name", "Client", "Project", "Date", "Description", "Work Hours", "Out of Hours", "Total Hours",
            "To Payroll", "Approved for Payroll", "Sent to Payroll", "Posting Batch",
        ]));

        foreach (var e in rows)
        {
            csv.AppendLine(string.Join(',', [
                CsvField(e.User?.DisplayName),
                CsvField(e.Client?.Name),
                CsvField(e.Project?.Name),
                CsvField(e.Date.ToString("yyyy-MM-dd")),
                CsvField(e.Description),
                CsvField(e.WorkHours.ToString()),
                CsvField(e.OutOfHoursHours.ToString()),
                CsvField((e.WorkHours + e.OutOfHoursHours).ToString()),
                CsvField(e.ToPayroll.ToString("0.00")),
                CsvField(e.ApprovedPayroll ? "Yes" : "No"),
                CsvField(e.SentToPayroll ? "Yes" : "No"),
                CsvField(e.PostingBatch),
            ]));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new FileContentResult(bytes, "text/csv") { FileDownloadName = $"timesheet-export-{DateTime.UtcNow:yyyyMMdd}.csv" };
    }

    private static DateOnly? ParseDate(HttpRequest req, string key) =>
        req.Query.TryGetValue(key, out var v) && DateOnly.TryParse(v, out var d) ? d : null;

    private static int? ParseInt(HttpRequest req, string key) =>
        req.Query.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : null;

    /// <summary>Wraps in quotes and escapes embedded quotes whenever the value could otherwise break CSV
    /// parsing (contains a comma, quote, or newline) - descriptions are free text and can contain any of these.</summary>
    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
