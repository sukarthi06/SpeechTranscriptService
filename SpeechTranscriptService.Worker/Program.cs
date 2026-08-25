using Serilog;
using Serilog.Sinks.OpenTelemetry;
using SpeechTranscriptService.Application;
using SpeechTranscriptService.Infra;
using SpeechTranscriptService.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppService(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);

#region Serilog
var otlpEndpoint = builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317";
var otlpProtocol = builder.Configuration["Otlp:Protocol"] ?? "grpc";
var otlpHeaders = builder.Configuration["Otlp:Headers"];
var serviceName = builder.Configuration["Serilog:Properties:Application"] ?? "SpeechTranscriptService";
var serilogProtocol = otlpProtocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
    ? OtlpProtocol.HttpProtobuf
    : OtlpProtocol.Grpc;

builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otlpEndpoint;
            options.Protocol = serilogProtocol;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = serviceName
            };
            if (!string.IsNullOrEmpty(otlpHeaders))
            {
                options.Headers = otlpHeaders
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(kv => kv.Split('=', 2))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
            }
        });
});
#endregion

builder.Services.AddHostedService<SpeechTranscriptWorker>();

var host = builder.Build();
host.Run();
