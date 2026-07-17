namespace SpeechTranscriptService.Domain.Entities;

public class TranscriptSegment
{
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = "";
}
