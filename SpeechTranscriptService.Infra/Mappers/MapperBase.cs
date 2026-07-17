using Google.Protobuf.WellKnownTypes;

namespace SpeechTranscriptService.Infra.Mappers;

public abstract class MapperBase
{
    // protected, not private — must be visible to derived mapper classes
    protected DateTime MapTimestamp(Timestamp ts) => ts.ToDateTime();
    protected DateTime? MapNullableTimestamp(Timestamp? ts) => ts?.ToDateTime();
    protected Timestamp MapTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
    protected Timestamp? MapNullableTimestamp(DateTime? dt) =>
        dt.HasValue ? Timestamp.FromDateTime(DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc)) : null;

    protected Guid ParseGuid(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            throw new ArgumentException($"'{id}' is not a valid id.", nameof(id));
        return guid;
    }
}
