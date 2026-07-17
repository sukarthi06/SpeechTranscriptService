using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeechTranscriptService.Infra.Interfaces;

namespace SpeechTranscriptService.Infra.Services;

public sealed class AzureAudioObjectStorage(
        BlobServiceClient blobServiceClient,
        IOptions<AzureBlobStorageOptions> options,
        ILogger<AzureAudioObjectStorage> logger) : IAudioObjectStorage
{
    private readonly AzureBlobStorageOptions _options = options.Value;
    public async Task<long> GetLengthAsync(string path, CancellationToken ct)
    {
        var blobClient = blobServiceClient
            .GetBlobContainerClient(_options.SourceContainer)
            .GetBlobClient(path);
        if (!await blobClient.ExistsAsync(ct))
        {
            logger.LogInformation("Could not find Azure blob storage path: {@Path}", path);
            return 0;
        }
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
        return properties.Value.ContentLength;
    }
    public async Task<Stream> OpenReadStreamAsync(string path, CancellationToken ct)
    {
        var blobClient = blobServiceClient
            .GetBlobContainerClient(_options.SourceContainer)
            .GetBlobClient(path);

        // OpenReadAsync streams lazily from the blob rather than downloading
        // it into memory upfront - required given source sizes.
        return await blobClient.OpenReadAsync(cancellationToken: ct);
    }
}

