using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Application.Interfaces;

public interface ITranscriptConsolidator
{
    Task<string> ConsolidateTranscriptAsync(RecordingId recordingId, CancellationToken cancellationToken);
}
