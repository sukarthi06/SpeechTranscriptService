using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Application.Interfaces;

public interface IRecordingChunkServices
{
    Task<RecordingChunk> GetRecordingChunkAsync(ChunkId chunkId, CancellationToken cancellationToken);
}
