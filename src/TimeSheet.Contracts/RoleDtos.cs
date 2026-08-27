namespace TimeSheet.Contracts;

public record RoleDto(int Id, string Name, bool IsActive);

public record CreateRoleRequest(string Name);

public record UpdateRoleRequest(string Name, bool IsActive);
