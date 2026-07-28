using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SpeechTranscriptService.Application.Services;

public class TranscriptService(HttpClient httpClient,
    IOptions<OpenAIOptions> options,
    ILogger<TranscriptService> logger) : ITranscriptService
{
    public async Task<TranscriptionResponse> TranscribeAsync(Stream wavStream, ChunkId chunkId, CancellationToken cancellationToken)
    {
        try
        {
            if (!wavStream.CanSeek)
            {
                throw new InvalidOperationException("wavStream must be seekable for retry support.");
            }

            var policy = Policy
                .Handle<HttpRequestException>()
                .Or<IOException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))
                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)),
                    onRetry: (exception, delay, attempt, context) =>
                    {
                        logger.LogWarning(
                            exception,
                            "Transcription retry {Attempt} for ChunkId {ChunkId} after {Delay}ms",
                            attempt, chunkId, delay.TotalMilliseconds);
                    });

            HttpResponseMessage response = null!;

            await policy.ExecuteAsync(async () =>
            {
                wavStream.Seek(0, SeekOrigin.Begin);

                using var content = new MultipartFormDataContent();
                var audioContent = new StreamContent(wavStream);
                audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                content.Add(audioContent, "file", "audio.wav");
                content.Add(new StringContent("whisper-1"), "model");
                content.Add(new StringContent("en"), "language");
                content.Add(new StringContent("verbose_json"), "response_format");
                content.Add(new StringContent("segment"), "timestamp_granularities[]");

                response = await httpClient.PostAsync(options.Value.BaseUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();
            });

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("Whisper Status: {StatusCode}", response.StatusCode);
            return ExtractText(responseString);
        }
        catch (Exception)
        {
            logger.LogError("Transcription failed for ChunkId: {ChunkId}", chunkId);
            throw;
        }
    }
    private TranscriptionResponse ExtractText(string json)
    {
        var result = JsonSerializer.Deserialize<TranscriptionResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        //if (result != null) logger.LogInformation("Transcript: {Transcript}", JsonSerializer.Serialize(result));

        return result ?? new TranscriptionResponse();
    }
}
