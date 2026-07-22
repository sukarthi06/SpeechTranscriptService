using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Application.Interfaces;

public interface IRecordingService
{
    Task<bool> UpdateTranscriptPathAsync(RecordingId recordingId, string transcriptPath, CancellationToken cancellationToken);
}
