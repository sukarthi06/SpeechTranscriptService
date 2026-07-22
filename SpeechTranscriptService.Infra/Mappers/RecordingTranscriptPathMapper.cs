using RecordingGrpcService.Grpc.Protos;
using Riok.Mapperly.Abstractions;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Infra.Mappers;

[Mapper]
public partial class RecordingTranscriptPathMapper : MapperBase
{    
    [MapperIgnoreTarget(nameof(RecordingSessionDto.Status))]
    [MapperIgnoreTarget(nameof(RecordingSessionDto.StartedAt))]
    [MapperIgnoreTarget(nameof(RecordingSessionDto.StoppedAt))]
    [MapperIgnoreTarget(nameof(RecordingSessionDto.StoragePath))]
    [MapperIgnoreTarget(nameof(RecordingSessionDto.WavPath))]
    [MapperIgnoreTarget(nameof(RecordingSessionDto.Metadata))]
    public partial RecordingSessionDto ToDto(RecordingTranscriptPath dto);
    public string MapRecordingId(RecordingId id) => id.Value.ToString();


}
