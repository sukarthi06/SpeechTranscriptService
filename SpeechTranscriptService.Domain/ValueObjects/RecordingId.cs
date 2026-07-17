namespace SpeechTranscriptService.Domain.ValueObjects;

public record RecordingId
{
    public Guid Value { get; }
    private RecordingId(Guid value) => Value = value;

    public static RecordingId Of(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("RecordingId cannot be empty.", nameof(value));
        }
        return new RecordingId(value);
    }
}
