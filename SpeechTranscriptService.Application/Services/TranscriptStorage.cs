using Microsoft.Extensions.Logging;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;

namespace SpeechTranscriptService.Application.Services;

public class TranscriptStorage(
    ITranscriptObjectStorage transcriptStorage,
    IRecordingChunkGrpcClient recordingChunkGrpcClient,
    ILogger<TranscriptStorage> logger) : ITranscriptStorage
{
    public async Task<string?> SaveAsync(
        TranscriptionResponse transcription, RecordingId recordingId, ChunkId chunkId, CancellationToken ct)
    {
        string path = $"{DateTime.UtcNow:yyyy-MM-dd}/{recordingId}/{chunkId}.json"; ;
        var result = await transcriptStorage.UploadTranscriptAsync(path, transcription, ct);

        if (result)
        {
            logger.LogInformation("Transcript stored for ChunkId: {ChunkId}", chunkId);
            return path;
        }
        else { 
            logger.LogWarning("Failed to store Transcript for ChunkId: {ChunkId}", chunkId);
            return null;
        }
    }

    public async Task<bool> UpdateTranscriptPathAsync(ChunkId chunkId, string transcriptStoragePath, CancellationToken cancellationToken)
    {
        var result = await recordingChunkGrpcClient.UpdateTranscriptPathAsync(chunkId, transcriptStoragePath, cancellationToken);
        if (!result) logger.LogWarning("Transcript storage path update failed for ChunkId: {ChunkId}", chunkId);
        return result;
    }
    
    public async Task<TranscriptionResponse?> DownloadTranscriptAsync(ChunkId chunkId, string path, CancellationToken cancellationToken)
    {
        var response = await transcriptStorage.DownloadTranscriptAsync(chunkId, path, cancellationToken);
        throw new NotImplementedException();
    }

    public async Task<bool> UploadRecordingTranscriptAsync(
        RecordingTranscript recordingTranscript, string destinationPath, CancellationToken cancellationToken)
    {
        var response = await transcriptStorage.UploadRecordingTranscriptAsync(
            destinationPath, recordingTranscript, cancellationToken);
        return response;
    }
}
