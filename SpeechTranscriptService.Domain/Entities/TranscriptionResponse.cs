namespace SpeechTranscriptService.Domain.Entities;

public class TranscriptionResponse
{
    public string Text { get; set; } = string.Empty;
    public List<TranscriptSegment> Segments { get; set; } = [];
}
