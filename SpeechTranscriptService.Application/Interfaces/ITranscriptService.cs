using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Application.Interfaces;

public interface ITranscriptService
{
    Task<TranscriptionResponse> TranscribeAsync(Stream wavStream, ChunkId chunkId, CancellationToken cancellationToken);
}
