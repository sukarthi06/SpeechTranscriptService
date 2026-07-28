using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Polly;
using Polly.Extensions.Http;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Application.Services;
using SpeechTranscriptService.Domain.Entities;
using System.Net.Http.Headers;

namespace SpeechTranscriptService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAppService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<RecyclableMemoryStreamManager>();

        services.AddScoped<IRecordingChunkServices, RecordingChunkServices>();
        services.AddScoped<IWavStorageReader, WavStorageReader>();
        services.AddScoped<ITranscriptStorage, TranscriptStorage>();
        services.AddScoped<ITranscriptConsolidator, TranscriptConsolidator>();
        services.AddScoped<ITranscriptMerger, TranscriptMerger>();
        services.AddScoped<IRecordingService, RecordingService>();

        #region "OpenAI"

        services.Configure<OpenAIOptions>(
            configuration.GetSection(OpenAIOptions.SectionName));

        services.AddHttpClient<ITranscriptService, TranscriptService>((sp, client) =>
        {
            var options = sp
                .GetRequiredService<IOptions<OpenAIOptions>>()
                .Value;
            Console.WriteLine("Configuring HttpClient with BaseUrl: " + options.BaseUrl);
            Console.WriteLine("Using API Key: " + (string.IsNullOrEmpty(options.ApiKey) ? "Not Set" : "Set"));
            client.BaseAddress = new Uri(options.BaseUrl);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey);

            client.Timeout = TimeSpan.FromMinutes(2);
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<TranscriptService>>();

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<IOException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))
                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)),
                    onRetry: (outcome, delay, attempt, context) =>
                    {
                        if (outcome.Exception is not null)
                        {
                            logger.LogWarning(
                                outcome.Exception,
                                "Transcription request retry {Attempt} after {Delay}ms due to exception",
                                attempt, delay.TotalMilliseconds);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Transcription request retry {Attempt} after {Delay}ms due to status {StatusCode}",
                                attempt, delay.TotalMilliseconds, outcome.Result?.StatusCode);
                        }
                    });
        });

        #endregion

        return services;
    }
}
