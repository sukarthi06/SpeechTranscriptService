using Microsoft.Extensions.Logging;
using Microsoft.IO;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;

namespace SpeechTranscriptService.Application.Services;

public class WavStorageReader(
    IRecordingChunkServices recordingChunkServices,
    IAudioObjectStorage storage,
    RecyclableMemoryStreamManager streamManager,
    ILogger<WavStorageReader> logger) : IWavStorageReader
{    
    public async Task<Stream?> GetWavStreamAsync(ChunkId chunkId, CancellationToken cancellationToken)
    {
        var chunk = await recordingChunkServices.GetRecordingChunkAsync(chunkId, cancellationToken);

        if (string.IsNullOrEmpty(chunk.WavPath))
        {
            logger.LogWarning("Wav file path is empty for ChunkId: {ChunkId}", chunkId);
            throw new InvalidOperationException($"Wav file path is empty for ChunkId: {chunkId}");
        }

        var wavPath = chunk.WavPath;
        await using var storageReader = await storage.OpenReadStreamAsync(wavPath, cancellationToken);

        var chunkStream = streamManager.GetStream("chunk-read");
        await storageReader.CopyToAsync(chunkStream, cancellationToken);
        chunkStream.Position = 0;

        return chunkStream;
    }
}
