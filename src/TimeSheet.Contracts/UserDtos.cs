namespace TimeSheet.Contracts;

public record UserDto(int Id, string? EntraObjectId, bool IsLocalAccount, string Email, string DisplayName, string Role, int? JobRoleId, string? JobRoleName, bool IsActive, string PayrollNumber);

/// <summary>Admins pre-create User rows (invite-style). Provide EntraObjectId for an SSO account (matched up
/// on the person's first sign-in), or leave it null for a local account - a temporary password is generated
/// and returned once, the same way a password reset works. PayrollNumber is optional here (a placeholder is
/// generated if omitted, since the legacy schema requires some value) but should be filled in for real payroll
/// integration - see Users_Update for editing it later. JobRoleId is the job-function Role (Developer/
/// Consultant) their RateCard rates resolve against - see Role.cs.</summary>
public record InviteUserRequest(string? EntraObjectId, string Email, string DisplayName, string Role, int? JobRoleId, string? PayrollNumber);

public record InviteUserResponse(UserDto User, string? TemporaryPassword);

public record UpdateUserRequest(string DisplayName, string Role, int? JobRoleId, bool IsActive, string PayrollNumber);

public record MeDto(int Id, string DisplayName, string Email, string Role, bool IsProjectManager);
