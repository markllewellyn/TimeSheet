namespace TimeSheet.Contracts;

public record UserDto(int Id, string? EntraObjectId, bool IsLocalAccount, string Email, string DisplayName, string Role, string? JobTitle, bool IsActive);

/// <summary>Admins pre-create User rows (invite-style). Provide EntraObjectId for an SSO account (matched up
/// on the person's first sign-in), or leave it null for a local account - a temporary password is generated
/// and returned once, the same way a password reset works.</summary>
public record InviteUserRequest(string? EntraObjectId, string Email, string DisplayName, string Role, string? JobTitle);

public record InviteUserResponse(UserDto User, string? TemporaryPassword);

public record UpdateUserRequest(string DisplayName, string Role, string? JobTitle, bool IsActive);

public record MeDto(int Id, string DisplayName, string Email, string Role);
