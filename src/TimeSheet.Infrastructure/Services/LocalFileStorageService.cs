using Microsoft.Extensions.Configuration;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Local filesystem storage: writes under {AttachmentsRootPath}/{yyyy}/{MM}/{guid}-{sanitizedFileName}.
/// StorageKey is the relative path fragment (never absolute), so it stays portable if the root moves and
/// callers never construct filesystem paths themselves - this is what keeps the later Blob Storage swap a
/// single-class change.
/// </summary>
public class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private string RootPath => configuration["AttachmentsRootPath"]
        ?? throw new InvalidOperationException("Missing AttachmentsRootPath configuration.");

    public async Task<string> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct)
    {
        var storageKey = AttachmentStorageKeyBuilder.Build(suggestedFileName);
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, storageKey));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(storageKey);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveFullPath(string storageKey)
    {
        // Guard against path traversal - a storage key should only ever be one we generated ourselves.
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, storageKey));
        var rootFullPath = Path.GetFullPath(RootPath);
        if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid storage key.");
        }
        return fullPath;
    }
}
