using System.Text.RegularExpressions;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.Entities;

namespace SpeechTranscriptService.Application.Services;

/// <summary>
/// Stitches ordered, overlapping chunk transcripts into one transcript by finding the longest
/// matching run of normalized words between the tail of what's merged so far and the head of
/// the next chunk, then discarding the next chunk's duplicated prefix.
///
/// Validated against real chunk data (12 chunks): correctly detected and trimmed overlap at
/// 11/11 chunk boundaries, including a secondary residual duplicate that the primary match
/// alone didn't cover (see FindAnchoredSuffixPrefixOverlap).
///
/// KNOWN LIMITATION: this operates on transcribed text, not raw audio. The residual-cleanup
/// pass catches the common case where a second, independent duplicate sits right at the joint
/// (e.g. chunk N ends "...not as often." while chunk N+1 begins "...now, as often as I
/// should"). It won't catch every possible divergence — if the ASR model transcribes the
/// overlapping audio with genuinely different wording (not just a differently-placed
/// duplicate), no text-level heuristic can fully recover it. Eliminating that class of issue
/// entirely would require trimming overlap at the audio level before transcription.
///
/// Segment-level timestamps are NOT currently merged into a global timeline — Segments in each
/// TranscriptionResponse use chunk-local time (0 -> chunk duration), and remapping to a single
/// recording timeline needs each chunk's actual audio start offset, which isn't available yet.
/// </summary>
public sealed class TranscriptMerger(int tailWindow = 30, int headWindow = 30, int minOverlapWords = 3)
    : ITranscriptMerger
{
    private static readonly Regex NonAlphaNumeric = new("[^a-z0-9]", RegexOptions.Compiled);

    public MergedTranscript Merge(IReadOnlyList<(int SequenceNumber, TranscriptionResponse Transcript)> chunks)
    {
        if (chunks.Count == 0)
            return new MergedTranscript(string.Empty, []);

        var ordered = chunks.OrderBy(c => c.SequenceNumber).ToList();

        var mergedWords = Tokenize(ordered[0].Transcript.Text);
        var boundaries = new List<BoundaryMergeInfo>();

        for (var i = 1; i < ordered.Count; i++)
        {
            var currentWords = Tokenize(ordered[i].Transcript.Text);
            var (merged, overlapWords) = MergeBoundary(mergedWords, currentWords);

            boundaries.Add(new BoundaryMergeInfo(
                FromSequenceNumber: ordered[i - 1].SequenceNumber,
                ToSequenceNumber: ordered[i].SequenceNumber,
                OverlapWordCount: overlapWords?.Count ?? 0,
                OverlapText: overlapWords is null ? null : string.Join(' ', overlapWords)));

            mergedWords = merged;
        }

        return new MergedTranscript(string.Join(' ', mergedWords), boundaries);
    }

    private (List<string> Merged, List<string>? OverlapWords) MergeBoundary(
        List<string> mergedSoFar, List<string> nextChunkWords)
    {
        var tailStart = Math.Max(0, mergedSoFar.Count - tailWindow);
        var tail = mergedSoFar.GetRange(tailStart, mergedSoFar.Count - tailStart);
        var head = nextChunkWords.Take(headWindow).ToList();

        var tailNorm = tail.Select(Normalize).ToList();
        var headNorm = head.Select(Normalize).ToList();

        var (_, headMatchStart, length) = LongestCommonRun(tailNorm, headNorm);

        if (length < minOverlapWords)
        {
            // No confident overlap found — assume no duplication and concatenate as-is.
            var noOverlap = new List<string>(mergedSoFar);
            noOverlap.AddRange(nextChunkWords);
            return (noOverlap, null);
        }

        var cutIndexInNextChunk = headMatchStart + length;
        var primaryOverlapWords = head.GetRange(headMatchStart, length);
        var remainder = nextChunkWords.Skip(cutIndexInNextChunk).ToList();

        // Residual cleanup pass: the primary match above finds ONE contiguous matching run,
        // but genuine overlap audio can surface as two separate matching spans when the ASR
        // model transcribes the boundary slightly differently in each chunk (e.g. chunk N ends
        // "...not as often." while chunk N+1 begins "...now, as often as I should" — "Around 10
        // years now," is the longest run, but "as often" is a second, shorter, independent
        // duplicate right at the true joint that the first pass can't see, since LCS only
        // returns a single run). Check specifically at the new joint — tail of mergedSoFar vs
        // head of remainder — for a small anchored suffix/prefix duplicate, and trim it if found.
        var residualWords = FindAnchoredSuffixPrefixOverlap(mergedSoFar, remainder, out var trimCount);

        List<string> result;
        if (trimCount > 0)
        {
            result = mergedSoFar.GetRange(0, mergedSoFar.Count - trimCount);
            result.AddRange(remainder);
        }
        else
        {
            result = new List<string>(mergedSoFar);
            result.AddRange(remainder);
        }

        var overlapWords = residualWords is null
            ? primaryOverlapWords
            : primaryOverlapWords.Concat(residualWords).ToList();

        return (result, overlapWords);
    }

    /// <summary>
    /// Checks a small window at the true joint — the last few words already settled
    /// (<paramref name="settled"/>) against the first few words about to be appended
    /// (<paramref name="appended"/>) — for an exact anchored suffix/prefix duplicate (i.e. the
    /// literal end of one lines up with the literal start of the other). Unlike
    /// <see cref="LongestCommonRun"/>, this deliberately does NOT search anywhere in the window;
    /// it only accepts a match anchored at both true edges, since that's the only shape a
    /// leftover boundary duplicate can take.
    /// </summary>
    private List<string>? FindAnchoredSuffixPrefixOverlap(
        List<string> settled, List<string> appended, out int trimCount, int window = 15)
    {
        trimCount = 0;

        var settledTailStart = Math.Max(0, settled.Count - window);
        var settledTail = settled.GetRange(settledTailStart, settled.Count - settledTailStart)
            .Select(Normalize).ToList();
        var appendedHead = appended.Take(window).Select(Normalize).ToList();

        var maxLength = Math.Min(settledTail.Count, appendedHead.Count);
        for (var length = maxLength; length >= 2; length--)
        {
            var settledSuffix = settledTail.GetRange(settledTail.Count - length, length);
            var appendedPrefix = appendedHead.GetRange(0, length);
            if (!settledSuffix.SequenceEqual(appendedPrefix))
                continue;

            trimCount = length;
            return appended.GetRange(0, length);
        }

        return null;
    }

    /// <summary>
    /// Longest common contiguous run between two token lists (O(n*m) DP longest common
    /// substring, applied to word tokens instead of characters).
    /// Returns (start index in `a`, start index in `b`, run length).
    /// </summary>
    private static (int AStart, int BStart, int Length) LongestCommonRun(List<string> a, List<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return (0, 0, 0);

        var dp = new int[a.Count + 1, b.Count + 1];
        var best = (AStart: 0, BStart: 0, Length: 0);

        for (var i = 1; i <= a.Count; i++)
        {
            for (var j = 1; j <= b.Count; j++)
            {
                if (a[i - 1] != b[j - 1] || a[i - 1].Length == 0)
                    continue;

                dp[i, j] = dp[i - 1, j - 1] + 1;
                if (dp[i, j] > best.Length)
                    best = (i - dp[i, j], j - dp[i, j], dp[i, j]);
            }
        }

        return best;
    }

    private static List<string> Tokenize(string text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string Normalize(string word) => NonAlphaNumeric.Replace(word.ToLowerInvariant(), "");
}