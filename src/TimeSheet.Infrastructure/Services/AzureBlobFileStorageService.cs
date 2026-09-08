using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage implementation of IFileStorageService (Azurite locally, via the same
/// AzureWebJobsStorage connection string the Functions host already uses for its timer triggers - no
/// separate blob-specific config needed). StorageKey is the blob name, same {yyyy}/{MM}/{guid}-{fileName}
/// shape LocalFileStorageService used - blob names support '/' natively as virtual-folder delimiters, so no
/// directory-creation step is needed the way local disk required.
/// </summary>
public class AzureBlobFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private const string ContainerName = "attachments";
    private BlobContainerClient? containerClient;

    private BlobContainerClient ContainerClient => containerClient ??= new BlobContainerClient(
        configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("Missing AzureWebJobsStorage configuration."),
        ContainerName);

    public async Task<string> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct)
    {
        await ContainerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var storageKey = AttachmentStorageKeyBuilder.Build(suggestedFileName);
        await ContainerClient.UploadBlobAsync(storageKey, content, ct);

        return storageKey;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var response = await ContainerClient.GetBlobClient(storageKey).DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct) =>
        await ContainerClient.GetBlobClient(storageKey).DeleteIfExistsAsync(cancellationToken: ct);
}
