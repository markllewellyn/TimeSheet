using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Master-list CSV export for finance/payroll - a flat, row-level dump of every timesheet entry
/// across every staff member (not scoped to one client/project, unlike Reports, which is aggregate). No
/// legacy equivalent existed to port from (the Power App export was searched for Export/CSV/Excel/SaveData and
/// none was found) - built fresh, same column set as the Approvals/Overview screens use.</summary>
public class AdminExportFunctions(ITimesheetEntryRepository entries, ICurrentUserAccessor currentUser)
{
    [Function("Admin_ExportTimesheetEntries")]
    public async Task<IActionResult> ExportTimesheetEntries(
        // Deliberately not "admin/..." - Azure Functions reserves that path prefix for its own host API and
        // silently drops user-defined routes that start with it (same constraint already noted on
        // Users_ResetPassword in UsersFunctions.cs).
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "exports/timesheet-entries")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var from = req.Query.TryGetValue("from", out var f) && DateOnly.TryParse(f, out var fd) ? fd : (DateOnly?)null;
        var to = req.Query.TryGetValue("to", out var t) && DateOnly.TryParse(t, out var td) ? td : (DateOnly?)null;

        var rows = await entries.GetAllInRangeAsync(from, to, ct);

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

    /// <summary>Wraps in quotes and escapes embedded quotes whenever the value could otherwise break CSV
    /// parsing (contains a comma, quote, or newline) - descriptions are free text and can contain any of these.</summary>
    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
