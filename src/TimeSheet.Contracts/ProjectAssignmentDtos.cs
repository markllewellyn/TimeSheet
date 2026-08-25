namespace TimeSheet.Contracts;

public record ProjectAssignmentDto(
    int Id, int ProjectId, string ProjectName, int UserId, string UserDisplayName,
    string Status, DateOnly StartDate, DateOnly? EndDate, decimal? AllocatedHoursPerWeek, string? Notes);

public record CreateProjectAssignmentRequest(int ProjectId, int UserId, DateOnly StartDate, decimal? AllocatedHoursPerWeek, string? Notes);

public record UpdateProjectAssignmentRequest(string Status, DateOnly? EndDate, decimal? AllocatedHoursPerWeek, string? Notes);

public record ProjectAllocationLineDto(int ProjectId, string ProjectName, string ClientName, decimal AllocatedHoursPerWeek);

public record EstimatedWeeklyWorkloadDto(
    DateOnly WeekStart, decimal EstimatedHoursThisWeek,
    IReadOnlyList<ProjectAllocationLineDto> ByProject, IReadOnlyList<int> AssignmentsMissingAllocation);
