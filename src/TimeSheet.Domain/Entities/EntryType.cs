namespace TimeSheet.Domain.Entities;

/// <summary>
/// FDD: "The entry types configured against a project, controlling what can be selected on an entry." Admin-
/// defined per project (no fixed system list) - genuinely new, no legacy equivalent. IsContractType gates the
/// FDD rule "only projects of type Contract accept contract entries" (see Project.ProjectType); deactivated
/// (IsActive=false) rather than deleted, since a historical TimesheetEntry may still reference it.
/// </summary>
public class EntryType
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public required string Name { get; set; }
    public bool IsContractType { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
}
