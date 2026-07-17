using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Domain.Entities;

public class RecordingChunk
{
    public ChunkId ChunkId { get; set; } = default!;
    public RecordingId RecordingId { get; set; } = default!;
    public int SequenceNumber { get; set; } = default!;
    public string StoragePath { get; set; } = default!;
    public string? WavPath { get; set; }
    public string? TranscriptPath { get; set; }
}
