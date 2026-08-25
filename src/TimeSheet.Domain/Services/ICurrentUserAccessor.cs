namespace TimeSheet.Domain.Services;

public record CurrentUserContext(int UserId, string EntraObjectId, string Email, string DisplayName, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
}

/// <summary>Exposes the app User resolved from the caller's Entra `oid` claim (see CurrentUserMiddleware in
/// TimeSheet.Api). Authorization is decided against this — the app's own database — not Entra App Roles.</summary>
public interface ICurrentUserAccessor
{
    /// <summary>Null only for anonymous endpoints; every authenticated request is guaranteed a resolved user
    /// by the time it reaches a Function (CurrentUserMiddleware returns 403 "not provisioned" otherwise).</summary>
    CurrentUserContext? Current { get; }
}
