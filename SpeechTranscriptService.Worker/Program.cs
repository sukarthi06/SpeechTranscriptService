using Serilog;
using SpeechTranscriptService.Application;
using SpeechTranscriptService.Infra;
using SpeechTranscriptService.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppService(builder.Configuration);

#region Serilog
builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});
#endregion

builder.Services.AddHostedService<SpeechTranscriptWorker>();

var host = builder.Build();
host.Run();
