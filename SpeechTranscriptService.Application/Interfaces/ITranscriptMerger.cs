using SpeechTranscriptService.Domain.Entities;

namespace SpeechTranscriptService.Application.Interfaces;

/// <summary>Diagnostic info about what was trimmed at each chunk boundary — log
/// OverlapWordCount == 0 cases, since that's the signal a boundary had no confident
/// overlap match (either the overlap window is too small, or that pair of chunks had
/// a divergent ASR transcription at the edge — see TranscriptMerger remarks).</summary>
public sealed record BoundaryMergeInfo(
    int FromSequenceNumber,
    int ToSequenceNumber,
    int OverlapWordCount,
    string? OverlapText);

public sealed record MergedTranscript(string Text, IReadOnlyList<BoundaryMergeInfo> Boundaries);

public interface ITranscriptMerger
{
    /// <summary>
    /// Stitches ordered, overlapping chunk transcripts into one transcript. Chunks do not
    /// need to be pre-sorted; this orders by SequenceNumber defensively.
    /// </summary>
    MergedTranscript Merge(IReadOnlyList<(int SequenceNumber, TranscriptionResponse Transcript)> chunks);
}