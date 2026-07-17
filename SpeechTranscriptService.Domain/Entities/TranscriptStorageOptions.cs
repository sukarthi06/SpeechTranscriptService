namespace SpeechTranscriptService.Domain.Entities;

public sealed class TranscriptStorageOptions
{
    public string ContainerName { get; init; } = "transcripts";
}
