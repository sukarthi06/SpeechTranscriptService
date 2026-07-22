using RecordingGrpcService.Grpc.Protos;
using Riok.Mapperly.Abstractions;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Infra.Mappers;

[Mapper]
public partial class RecordingChunkMapper : MapperBase
{
    // ---- RecordingChunk ----
    [MapperIgnoreSource(nameof(RecordingChunkDto.StartTime))]
    [MapperIgnoreSource(nameof(RecordingChunkDto.EndTime))]
    [MapperIgnoreSource(nameof(RecordingChunkDto.ChunkDuration))]
    public partial RecordingChunk ToDomain(RecordingChunkDto dto);

    [MapperIgnoreTarget(nameof(RecordingChunkDto.StartTime))]
    [MapperIgnoreTarget(nameof(RecordingChunkDto.EndTime))]
    [MapperIgnoreTarget(nameof(RecordingChunkDto.ChunkDuration))]
    public partial RecordingChunkDto ToDto(RecordingChunk entity);

    // ---- List mapping (used for GetRecordingChunkResponse's repeated field) ----
    public partial List<RecordingChunk> ToDomainList(IEnumerable<RecordingChunkDto> dtos);
    public partial List<RecordingChunkDto> ToDtoList(IEnumerable<RecordingChunk> entities);

    // ---- ChunkId (string <-> value object) ----
    private ChunkId MapChunkId(string id) => ChunkId.Of(ParseGuid(id));
    public string MapChunkId(ChunkId id) => id.Value.ToString();

    // ---- RecordingId (string <-> value object) ----
    private RecordingId MapRecordingId(string id) => RecordingId.Of(ParseGuid(id));
    public string MapRecordingId(RecordingId id) => id.Value.ToString();
}
