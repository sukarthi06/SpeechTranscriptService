using Microsoft.Extensions.Logging;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;

namespace SpeechTranscriptService.Application.Services;

public class RecordingChunkServices(
    IRecordingChunkGrpcClient grpcClient,
    ILogger<RecordingChunkServices> logger) : IRecordingChunkServices
{
    public async Task<RecordingChunk> GetRecordingChunkAsync(ChunkId chunkId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching Recording Chunk for ChunkId: {ChunkId}", chunkId);
        return await grpcClient.GetRecordingChunkAsync(chunkId, cancellationToken);
    }

    public async Task<List<RecordingChunk>> GetRecordingChunksForConsolidationAsync(
        RecordingId recordingId, CancellationToken cancellationToken)
    {
        var chunks = await grpcClient.GetRecordingChunksAsync(recordingId, cancellationToken);
        return chunks
                .Select(rc => new RecordingChunk
                {
                    ChunkId = rc.ChunkId,
                    RecordingId = rc.RecordingId,
                    SequenceNumber = rc.SequenceNumber,
                    StoragePath = string.Empty,
                    WavPath = string.Empty,
                    TranscriptPath = rc.TranscriptPath ?? string.Empty
                })
                .OrderBy(rc => rc.SequenceNumber)
                .ToList();

    }

    public async Task<bool> IsChunksReadyForConsolidationAsync(RecordingId recordingId, CancellationToken cancellationToken)
    {        
        return await grpcClient.IsChunksReadyForConsolidationAsync(recordingId, cancellationToken);
    }
}
