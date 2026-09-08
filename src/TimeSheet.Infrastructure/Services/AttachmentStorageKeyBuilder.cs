namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Builds the {yyyy}/{MM}/{guid:N}-{sanitizedFileName} StorageKey shape shared by every IFileStorageService
/// implementation, so the key format stays identical regardless of backend. Windows-legal filenames
/// (Path.GetInvalidFileNameChars()) are a superset of Blob Storage's own looser naming rules, so the same
/// sanitization is safe to reuse for both local disk and blob names - no separate "lighter" blob sanitizer
/// needed.
/// </summary>
internal static class AttachmentStorageKeyBuilder
{
    public static string Build(string suggestedFileName)
    {
        var now = DateTimeOffset.UtcNow;
        var sanitized = SanitizeFileName(suggestedFileName);
        return $"{now.Year}/{now.Month:D2}/{Guid.NewGuid():N}-{sanitized}";
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
