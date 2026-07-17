namespace SpeechTranscriptService.Infra.Interfaces;

public interface IAudioObjectStorage
{
    /// <summary>Byte length of the object at <paramref name="path"/>.</summary>
    Task<long> GetLengthAsync(string path, CancellationToken ct);

    /// <summary>
    /// Opens a stream for chunked reading. Callers control chunk size via
    /// their own read buffer - the stream itself does not load the object
    /// into memory.
    /// </summary>
    Task<Stream> OpenReadStreamAsync(string path, CancellationToken ct);
}
