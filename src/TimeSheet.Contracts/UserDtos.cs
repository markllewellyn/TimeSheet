namespace TimeSheet.Contracts;

public record UserDto(int Id, string EntraObjectId, string Email, string DisplayName, string Role, string? JobTitle, bool IsActive);

/// <summary>Admins pre-create User rows (invite-style) - the EntraObjectId is looked up/entered once the
/// person's Entra account exists, matching them up on their first sign-in.</summary>
public record InviteUserRequest(string EntraObjectId, string Email, string DisplayName, string Role, string? JobTitle);

public record UpdateUserRequest(string DisplayName, string Role, string? JobTitle, bool IsActive);

public record MeDto(int Id, string DisplayName, string Email, string Role);
