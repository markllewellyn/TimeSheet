namespace TimeSheet.Contracts;

/// <summary>A staff member's assignment on one project, across all their projects (as opposed to
/// ProjectAssignmentDto, which is scoped to one project's assignees). Carries a resolved-rate preview -
/// FDD: "The screen shows, for any staff member, the rate that will be applied on each project they are
/// assigned to and whether that rate comes from their role or from an override." RateSource is a friendly
/// label derived from RateCardTier (see ProjectAssignmentsFunctions.ToRateSourceLabel), not the raw enum -
/// presentation-only, so the domain layer stays free of display concerns. RateWarning is set (with the rate
/// fields left null) when no rate could be resolved for this assignment, rather than failing the whole list.</summary>
public record StaffAssignmentDto(
    int Id, int ProjectId, string ProjectName, string ClientName, string Status,
    DateOnly StartDate, DateOnly? EndDate, decimal? AllocatedHoursPerWeek, string? Notes,
    decimal? ResolvedCustomerRate, string? RateSource, string? RateWarning);
