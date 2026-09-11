namespace TimeSheet.Contracts;

public record TeamDto(int Id, string Name, bool IsActive);

public record CreateTeamRequest(string Name);

public record UpdateTeamRequest(string Name, bool IsActive);
