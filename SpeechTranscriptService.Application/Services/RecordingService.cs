using Microsoft.Extensions.Logging;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;

namespace SpeechTranscriptService.Application.Services;

public class RecordingService(
    IRecordingGrpcClient recordingGrpcClient,
    ILogger<RecordingService> logger) : IRecordingService
{
    public async Task<bool> UpdateTranscriptPathAsync(
        RecordingId recordingId, string transcriptPath, CancellationToken cancellationToken)
    {
        var response = await recordingGrpcClient.UpdateTranscriptPathAsync(recordingId, transcriptPath, cancellationToken);
        
        if (response) 
            logger.LogInformation("Transcript Path updated succsessfully for RecordingId: {RecordingId}", recordingId);
        else 
            logger.LogWarning("Transcript Path update failed for RecordingId: {RecordingId}", recordingId);

        return response;
    }
}
