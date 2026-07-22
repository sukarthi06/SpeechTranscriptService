using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Infra.Interfaces;

public interface IRecordingChunkGrpcClient
{
    Task<RecordingChunk> GetRecordingChunkAsync(ChunkId chunkId, CancellationToken cancellationToken);
    Task<List<RecordingChunk>> GetRecordingChunksAsync(RecordingId recording, CancellationToken cancellationToken);
    Task<bool> UpdateTranscriptPathAsync(ChunkId chunkId, string transcriptStoragePath, CancellationToken cancellationToken);
    Task<bool> IsChunksReadyForConsolidationAsync(RecordingId recordingId, CancellationToken cancellationToken);
}
