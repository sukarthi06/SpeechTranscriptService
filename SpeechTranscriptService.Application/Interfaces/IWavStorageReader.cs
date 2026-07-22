using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Application.Interfaces;

public interface IWavStorageReader
{
    Task<Stream?> GetWavStreamAsync(ChunkId chunkId, CancellationToken cancellationToken);
}
