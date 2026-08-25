namespace TimeSheet.Domain.Services;

/// <summary>
/// Abstracts attachment content storage. LocalFileStorageService (Infrastructure) is today's implementation -
/// local filesystem only, since the whole app is local-only for now. StorageKey is an opaque string that
/// callers never construct/interpret themselves; swapping to Azure Blob Storage later is a single new
/// Infrastructure class, no Domain/Contracts/caller change.
/// </summary>
public interface IFileStorageService
{
    Task<string> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
