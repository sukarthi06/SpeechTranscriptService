using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["Otlp:Endpoint"] ?? "http://localhost:4317";
        var otlpProtocol = configuration["Otlp:Protocol"] ?? "grpc";
        var otlpHeaders = configuration["Otlp:Headers"];
        var serviceName = configuration["Serilog:Properties:Application"] ?? "SpeechTranscriptService";
        var exportProtocol = otlpProtocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;

        void ConfigureExporter(OtlpExporterOptions otlp)
        {
            otlp.Endpoint = new Uri(otlpEndpoint);
            otlp.Protocol = exportProtocol;
            if (!string.IsNullOrEmpty(otlpHeaders))
                otlp.Headers = otlpHeaders;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName))
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(ConfigureExporter))
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(ConfigureExporter));

        return services;
    }
}
