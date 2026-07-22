using Microsoft.Extensions.Logging;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;
using System.Text.Json;

namespace SpeechTranscriptService.Application.Services;

public class TranscriptConsolidator(
    IRecordingChunkServices recordingChunkServices,
    ITranscriptObjectStorage transcriptObjectStorage,
    ITranscriptMerger transcriptMerger,
    ILogger<TranscriptConsolidator> logger) : ITranscriptConsolidator
{
    private List<RecordingChunk>? _recordingChunks;
    List<(int SequenceNumber, TranscriptionResponse Transcript)>? _chunkTranscripts;

    public async Task<string> ConsolidateTranscriptAsync(RecordingId recordingId, CancellationToken cancellationToken)
    {
        _recordingChunks = await GetRecordingChunksAsync(recordingId, cancellationToken);

        _chunkTranscripts = new List<(int SequenceNumber, TranscriptionResponse Transcript)>(_recordingChunks.Count);
        foreach (var chunk in _recordingChunks)
        {
            var transcriptionResponse = await GetTranscriptionResponseAsync(chunk.ChunkId, chunk.TranscriptPath!, cancellationToken);
            _chunkTranscripts.Add((chunk.SequenceNumber, transcriptionResponse));
        }
        
        var mergedTranscript = transcriptMerger.Merge(_chunkTranscripts);

        foreach (var boundary in mergedTranscript.Boundaries)
        {
            if (boundary.OverlapWordCount == 0)
            {
                logger.LogWarning(
                    "No overlap detected between chunk sequence {From} and {To} for recording {RecordingId}; " +
                    "concatenated without trimming",
                    boundary.FromSequenceNumber, boundary.ToSequenceNumber, recordingId);
            }
        }
        
        return mergedTranscript.Text;
    }

    private async Task<List<RecordingChunk>> GetRecordingChunksAsync(RecordingId recordingId, CancellationToken cancellationToken)
    {
        return await recordingChunkServices.GetRecordingChunksForConsolidationAsync(recordingId, cancellationToken: cancellationToken);
    }

    private async Task<TranscriptionResponse> GetTranscriptionResponseAsync(ChunkId chunkId, 
        string path, CancellationToken cancellationToken)
    {
        var response = await transcriptObjectStorage.DownloadTranscriptAsync(chunkId, path, cancellationToken);
        return response is null ? new TranscriptionResponse() : response;
    }

    private async Task<bool> SyncSegmentsAsync(CancellationToken cancellationToken)
    {
        double ovrlapDuration = 3;
        double previousEnd = 0;
        if (_chunkTranscripts is null) return false;

        JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null, // preserve "Text"/"Segments"/"Start"/"End" casing on write
            PropertyNameCaseInsensitive = true // tolerate case mismatches on read (e.g. "text" vs "Text")
        };

        foreach (var transcript in _chunkTranscripts)
        {
            var segments = transcript.Transcript.Segments.OrderBy(s => s.Start);
            if(transcript.SequenceNumber == 1)
            {
                previousEnd = segments.Last().End;
            }
            else
            {
                previousEnd = previousEnd - ovrlapDuration;
                foreach (var segment in segments)
                {
                    segment.Start = previousEnd;
                    segment.End = previousEnd + segment.End;
                    previousEnd = segment.End;
                }
            }

            var test = JsonSerializer.Serialize(segments, JsonOptions);
        }
        throw new NotImplementedException();
    }
}