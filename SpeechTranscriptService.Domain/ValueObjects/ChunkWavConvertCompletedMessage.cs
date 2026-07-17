namespace SpeechTranscriptService.Domain.ValueObjects;

public record ChunkWavConvertCompletedMessage(
    Guid ChunkId,
    Guid RecordingId,
    DateTimeOffset CompletedAt);
