namespace TimeSheet.Contracts;

public record EntryTypeDto(int Id, int ProjectId, string Name, bool IsContractType, bool IsActive);

public record CreateEntryTypeRequest(string Name, bool IsContractType);

public record UpdateEntryTypeRequest(string Name, bool IsActive);
