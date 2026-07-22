using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Infra.Interfaces;

public interface IRecordingGrpcClient
{
    public Task<bool> UpdateTranscriptPathAsync(RecordingId recordingId, string transcriptPath, CancellationToken cancellationToken);
}
