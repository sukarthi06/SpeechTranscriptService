using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;

namespace SpeechTranscriptService.Application.Interfaces;

public interface ITranscriptStorage
{
    Task<string?> SaveAsync(TranscriptionResponse transcription, RecordingId recordingId, ChunkId chunkId, CancellationToken ct);
    Task<bool> UpdateTranscriptPathAsync(ChunkId chunkId, string transcriptStoragePath, CancellationToken cancellationToken);
    Task<TranscriptionResponse?> DownloadTranscriptAsync(ChunkId chunkId, string path, CancellationToken cancellationToken);
    Task<bool> UploadRecordingTranscriptAsync(
        RecordingTranscript recordingTranscript, string destinationPath, CancellationToken cancellationToken);
}
