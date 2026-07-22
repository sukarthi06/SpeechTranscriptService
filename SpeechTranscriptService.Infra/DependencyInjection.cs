using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecordingGrpcService.Grpc.Protos;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Infra.Grpc;
using SpeechTranscriptService.Infra.Interfaces;
using SpeechTranscriptService.Infra.Mappers;
using SpeechTranscriptService.Infra.Services;

namespace SpeechTranscriptService.Infra;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        #region RabbitMq

        services.AddSingleton<IMessageConsumer>(sp =>
            RabbitMqConsumer.CreateAsync(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ILogger<RabbitMqConsumer>>()
            ).GetAwaiter().GetResult());

        services.AddSingleton<IMessagePublisher>(sp =>
            RabbitMqPublisher.CreateAsync(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ILogger<RabbitMqPublisher>>()
            ).GetAwaiter().GetResult());

        #endregion

        #region "Grpc"

        services.AddGrpcClient<RecordingChunkService.RecordingChunkServiceClient>(o =>
        {
            o.Address = new Uri(configuration["RecordingGrpcService:Address"]!);
        });
        services.AddSingleton<RecordingChunkMapper>();        
        services.AddScoped<IRecordingChunkGrpcClient, RecordingChunkGrpcClient>();
        

        services.AddGrpcClient<RecordingService.RecordingServiceClient>(o =>
        {
            o.Address = new Uri(configuration["RecordingGrpcService:Address"]!);
        });
        services.AddSingleton<RecordingTranscriptPathMapper>();
        services.AddScoped<IRecordingGrpcClient, RecordingGrpcClient>();

        #endregion

        #region "Blob"

        services.Configure<AzureBlobStorageOptions>(
            configuration.GetSection(AzureBlobStorageOptions.SectionName));
        services.AddSingleton<BlobServiceClient>(sp =>
            new BlobServiceClient(configuration["Azure:StorageConnectionString"]));
        services.AddSingleton<IAudioObjectStorage, AzureAudioObjectStorage>();

        services.Configure<TranscriptStorageOptions>(configuration.GetSection("Azure:TranscriptStorage"));
        services.AddSingleton<ITranscriptObjectStorage, AzureBlobTranscriptStorage>();

        #endregion

        return services;
    }
}
