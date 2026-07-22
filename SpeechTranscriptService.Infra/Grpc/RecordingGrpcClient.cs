using RecordingGrpcService.Grpc.Protos;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;
using SpeechTranscriptService.Infra.Mappers;

namespace SpeechTranscriptService.Infra.Grpc;

public class RecordingGrpcClient(
    RecordingService.RecordingServiceClient grpcClient,
    RecordingTranscriptPathMapper mapper) : IRecordingGrpcClient
{
    public async Task<bool> UpdateTranscriptPathAsync(
        RecordingId recordingId, string transcriptPath, CancellationToken cancellationToken)
    {
        var response = await grpcClient.UpdateTranscriptPathAsync(
            new UpdateTranscriptPathRequest { RecordingId = mapper.MapRecordingId(recordingId), TranscriptPath = transcriptPath },
            cancellationToken: cancellationToken);
        return response.IsSuccess;
    }
}
