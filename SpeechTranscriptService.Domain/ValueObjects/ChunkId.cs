namespace SpeechTranscriptService.Domain.ValueObjects;

public record ChunkId
{
    public Guid Value { get; }
    private ChunkId(Guid value) => Value = value;

    public static ChunkId Of(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ChunkId cannot be empty.", nameof(value));
        }
        return new ChunkId(value);
    }
}
