using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            using var content = new MultipartFormDataContent();

            var audioContent = new StreamContent(wavStream);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

            content.Add(audioContent, "file", "audio.wav");
            content.Add(new StringContent("whisper-1"), "model");
            content.Add(new StringContent("en"), "language");

            content.Add(new StringContent("verbose_json"), "response_format");
            content.Add(
                new StringContent("segment"),
                "timestamp_granularities[]"
            );
            //logger.LogInformation("Trascription Started for ChunkID: {ChunkID} at:{Time}", chunkId, DateTime.Now.ToString("hh:mm:ss"));
            var response = await httpClient.PostAsync(
                options.Value.BaseUrl,
                content,
                cancellationToken
            );
            logger.LogInformation("Whisper Status: {StatusCode}", response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            
            //response.EnsureSuccessStatusCode();            
            //logger.LogInformation("Trascription Ended for ChunkID: {ChunkID} at:{Time}", chunkId, DateTime.Now.ToString("hh:mm:ss"));
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
