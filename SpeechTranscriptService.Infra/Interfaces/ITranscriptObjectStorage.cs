using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Infra.Interfaces;

public interface ITranscriptObjectStorage
{
    Task<bool> UploadTranscriptAsync(
        string path,
        TranscriptionResponse transcript,
        CancellationToken cancellationToken);
}
