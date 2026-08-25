namespace TimeSheet.Domain.Entities;

public class User
{
    /// <summary>App's own surrogate key, deliberately decoupled from EntraObjectId (keeps the PK stable independent of the identity provider).</summary>
    public int Id { get; set; }

    /// <summary>The Entra `oid` claim — links this business record to the signed-in identity, for SSO accounts.
    /// Null for a local (username/password) account. Exactly one of EntraObjectId/PasswordHash is set - enforced
    /// in the service layer, not the schema, since SQLite's unique index already tolerates multiple NULLs here.</summary>
    public string? EntraObjectId { get; set; }

    /// <summary>PBKDF2 hash (self-contained: algorithm/iterations/salt/hash all encoded in this one string) for
    /// a local account. Null for an SSO account. Local accounts exist for people without an Entra identity in
    /// this tenant, and double as a fallback sign-in path if Entra SSO is ever misconfigured.</summary>
    public string? PasswordHash { get; set; }

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

    public bool IsLocalAccount => PasswordHash is not null;
}
