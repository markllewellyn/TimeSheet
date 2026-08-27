namespace TimeSheet.Contracts;

public record StaffCostDto(int Id, int StaffId, string StaffName, decimal HourlyCost, decimal OutOfHoursCost, DateOnly EffectiveFrom, DateTimeOffset CreatedUtc);

/// <summary>A "change" is always a new dated row - there is no update endpoint. See StaffCost.cs.</summary>
public record CreateStaffCostRequest(int StaffId, decimal HourlyCost, decimal OutOfHoursCost, DateOnly EffectiveFrom);
