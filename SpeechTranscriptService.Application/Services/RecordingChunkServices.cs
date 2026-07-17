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
}
