using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Domain.Entities;

public class RecordingTranscriptPath
{
    public RecordingId RecordingId { get; set; } = RecordingId.Of(Guid.NewGuid());
    public string TranscriptPath { get; set; } = default!;
}
