using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Infra.Interfaces;
using System.Text;
using System.Text.Json;

namespace SpeechTranscriptService.Infra.Services;

public sealed class AzureBlobTranscriptStorage(
    BlobServiceClient blobServiceClient,
    IOptions<TranscriptStorageOptions> options,
    ILogger<AzureBlobTranscriptStorage> logger) : ITranscriptObjectStorage
{
    private readonly BlobContainerClient _containerClient = blobServiceClient.GetBlobContainerClient(options.Value.ContainerName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null // preserve "Text"/"Segments"/"Start"/"End" casing as-is
    };

    public async Task<bool> UploadTranscriptAsync(
        string path,
        TranscriptionResponse transcript,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(transcript, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var stream = new MemoryStream(bytes, writable: false);

            if (!await _containerClient.ExistsAsync(cancellationToken))
                await _containerClient.CreateAsync();

            var blobClient = _containerClient.GetBlobClient(path);

            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                },
                cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload transcript");

            return false;
        }
    }
}
