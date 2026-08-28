namespace TimeSheet.Contracts;

/// <summary>A tenant member found via Entra directory search, cross-referenced against whether they already
/// have an app account - FDD: "an administrator can enable or disable any account within the SVG IT
/// tenancy." IsProvisioned/AppUserId/AppIsActive are null/false when nobody in the app's own User table has
/// this EntraObjectId yet.</summary>
public record TenantDirectoryUserDto(
    string EntraObjectId, string DisplayName, string? Email,
    bool IsProvisioned, int? AppUserId, bool? AppIsActive);
