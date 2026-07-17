using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Infra.Interfaces;

public interface IRecordingChunkGrpcClient
{
    Task<RecordingChunk> GetRecordingChunkAsync(ChunkId chunkId, CancellationToken cancellationToken);
    Task<bool> UpdateTranscriptPathAsync(ChunkId chunkId, string transcriptStoragePath, CancellationToken cancellationToken);
}
