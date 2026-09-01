namespace TimeSheet.Contracts;

public record PayrollPeriodDto(
    int Id, DateOnly PeriodStart, DateOnly PeriodEnd, DateTimeOffset GeneratedAtUtc,
    decimal TotalOutOfHoursHours, decimal TotalOutOfHoursPay, int StaffCount);

public record PayrollPeriodLineDto(int UserId, string StaffName, decimal OutOfHoursHours, decimal OutOfHoursPay);

public record PayrollPeriodDetailDto(
    int Id, DateOnly PeriodStart, DateOnly PeriodEnd, DateTimeOffset GeneratedAtUtc,
    decimal TotalOutOfHoursHours, decimal TotalOutOfHoursPay, IReadOnlyList<PayrollPeriodLineDto> Lines);
