using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;
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
        PropertyNamingPolicy = null, // preserve "Text"/"Segments"/"Start"/"End" casing on write
        PropertyNameCaseInsensitive = true // tolerate case mismatches on read (e.g. "text" vs "Text")
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

    public async Task<TranscriptionResponse?> DownloadTranscriptAsync(
        ChunkId chunkId, string path, CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(path);
        try
        {
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                logger.LogWarning(
                    "Transcript blob not found for ChunkId: {ChunkId}.", chunkId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToObjectFromJson<TranscriptionResponse>(JsonOptions);
        
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex, "Failed to deserialize transcript JSON for chunk {ChunkId}", chunkId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Failed to download transcript for chunk {ChunkId}", chunkId);
            return null;
        }
    }

    public async Task<bool> UploadRecordingTranscriptAsync(
        string path, RecordingTranscript recordingTranscript, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(recordingTranscript, JsonOptions);
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
