namespace TimeSheet.Contracts;

public record RateCardDto(
    int Id, int? RoleId, string? RoleName, int? StaffId, string? StaffName,
    int? ClientId, string? ClientName, int? ProjectId, string? ProjectName,
    decimal Rate, decimal? DiscountPercent, DateOnly EffectiveFrom, DateTimeOffset CreatedUtc);

/// <summary>Exactly one of RoleId/StaffId must be set; when StaffId is set, exactly one of ClientId/ProjectId
/// must be set (never both, never neither) - see RateCard.cs for the 5-tier scope shape. A "change" is always
/// a new dated row - there is no update endpoint. DiscountPercent (0-100) is the FDD's "discount applied
/// against a rate card before the invoice is produced" - optional, null means no discount.</summary>
public record CreateRateCardRequest(int? RoleId, int? StaffId, int? ClientId, int? ProjectId, decimal Rate, decimal? DiscountPercent, DateOnly EffectiveFrom);
