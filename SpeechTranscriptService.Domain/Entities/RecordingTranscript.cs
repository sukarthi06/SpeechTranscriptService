using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Domain.Entities;

public class RecordingTranscript
{
    public RecordingId RecordingId { get; set; } = default!;
    public string Text { get; set; } = string.Empty;
}
