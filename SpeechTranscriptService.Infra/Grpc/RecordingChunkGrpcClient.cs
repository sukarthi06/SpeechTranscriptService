using RecordingGrpcService.Grpc.Protos;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;
using SpeechTranscriptService.Infra.Mappers;

namespace SpeechTranscriptService.Infra.Grpc;

public class RecordingChunkGrpcClient(
    RecordingChunkService.RecordingChunkServiceClient grpcClient,
    RecordingChunkMapper mapper) : IRecordingChunkGrpcClient
{
    public async Task<RecordingChunk> GetRecordingChunkAsync(ChunkId chunkId, CancellationToken cancellationToken)
    {
        var response = await grpcClient.GetRecordingChunkAsync(
            new GetRecordingChunkRequest { ChunkId = mapper.MapChunkId(chunkId) },
            cancellationToken: cancellationToken);
        return mapper.ToDomain(response.RecordingChunk);
    }

    public async Task<bool> UpdateTranscriptPathAsync(ChunkId chunkId, string transcriptStoragePath, CancellationToken cancellationToken)
    {
        var response = await grpcClient.UpdateRecordingChunkTranscriptPathAsync(
            new UpdateRecordingChunkTranscriptPathRequest
            { ChunkId = mapper.MapChunkId(chunkId), TranscriptPath = transcriptStoragePath },
            cancellationToken: cancellationToken);
        return response.IsSuccess;
    }
}
