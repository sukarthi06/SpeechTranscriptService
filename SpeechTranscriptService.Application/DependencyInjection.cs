using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IO;
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
        });

        #endregion

        return services;
    }
}
