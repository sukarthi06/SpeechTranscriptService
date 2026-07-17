using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Application.Interfaces;

public interface ITranscriptStorage
{
    Task<string?> SaveAsync(TranscriptionResponse transcription, RecordingId recordingId, ChunkId chunkId, CancellationToken ct);
    Task<bool> UpdateTranscriptPathAsync(ChunkId chunkId, string transcriptStoragePath, CancellationToken cancellationToken);
}
