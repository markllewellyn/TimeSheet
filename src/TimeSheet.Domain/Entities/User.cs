namespace TimeSheet.Domain.Entities;

public class User
{
    /// <summary>App's own surrogate key, deliberately decoupled from EntraObjectId (keeps the PK stable independent of the identity provider).</summary>
    public int Id { get; set; }

    /// <summary>The Entra `oid` claim — links this business record to the signed-in identity.</summary>
    public required string EntraObjectId { get; set; }

    public required string Email { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>Flat, global role — NOT per-client. This is the app's own authorization source of truth.</summary>
    public UserRole Role { get; set; } = UserRole.User;

    public string? JobTitle { get; set; }

    /// <summary>Soft-disable leavers; preserves FK history on timesheets/assignments/rates.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    public List<ProjectAssignment> Assignments { get; set; } = [];
}
