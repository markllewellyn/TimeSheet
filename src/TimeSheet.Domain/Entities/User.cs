namespace TimeSheet.Domain.Entities;

/// <summary>
/// Maps onto the legacy [Staff] table (Id/DisplayName/Email/PayrollNumber/Role/IsActive, plus
/// PasswordHash/EntraObjectId added directly onto it for the app's dual local/SSO auth) plus a table-split
/// partner [StaffProfiles] holding audit fields, which have no legacy equivalent - see UserConfiguration.
/// HourlyCost/JobTitle have been replaced by the effective-dated StaffCost entity and the JobRoleId FK
/// respectively - see StaffCost and Role.
/// </summary>
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

    /// <summary>Legacy [Staff].PayrollNumber - no equivalent in the app's original design; required by the legacy schema.</summary>
    public required string PayrollNumber { get; set; }

    /// <summary>Flat, global role — NOT per-client. This is the app's own authorization source of truth.
    /// Stored as the legacy [Staff].Admin bit via a value conversion (UserRole has exactly two values). NOT to
    /// be confused with JobRoleId below - that's the FDD's job-function Role (Developer/Consultant), used for
    /// RateCard resolution; this is purely Admin-vs-User permissions.</summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>The job-function Role (e.g. Developer, Consultant) this person's role-tier RateCard rates
    /// resolve against - replaces the old free-text JobTitle. Nullable: a person with no Role assigned can only
    /// be billed via a person-level RateCard override (tiers 1/2), never a role tier (3/4/5).</summary>
    public int? JobRoleId { get; set; }
    public Role? JobRole { get; set; }

    /// <summary>The staff-grouping Team (e.g. "Delivery Pod A") this person currently belongs to - see Team.cs.
    /// Purely a reporting/filtering axis, no billing significance (unlike JobRoleId). Nullable: not every
    /// person needs a team.</summary>
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>Soft-disable leavers; preserves FK history on timesheets/assignments/costs.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    public bool IsLocalAccount => PasswordHash is not null;
}
