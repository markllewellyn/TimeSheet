using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface ITimesheetEntryRepository
{
    Task<TimesheetEntry?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The signed-in user's own entries (a User can only ever see/edit their own timesheet), optionally
    /// filtered by a free-text search over customer/project name and a date range - mirrors the legacy "Log
    /// Time" grid's search box.</summary>
    Task<IReadOnlyList<TimesheetEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct);

    Task AddAsync(TimesheetEntry entry, CancellationToken ct);
    void Update(TimesheetEntry entry);
    void Remove(TimesheetEntry entry);
}
