using RecordingGrpcService.Grpc.Protos;
using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;

namespace SpeechTranscriptService.Worker
{
    public class SpeechTranscriptWorker(
        IMessageConsumer messageConsumer,
        IServiceScopeFactory scopeFactory,
        ILogger<SpeechTranscriptWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            messageConsumer.MessageReceived += async consumed =>
            {
                try
                {
                    await ProcessAsync(consumed, stoppingToken);
                    await messageConsumer.AcknowledgeAsync(consumed.DeliveryTag, stoppingToken);
                }
                catch
                {
                    await messageConsumer.RejectAsync(consumed.DeliveryTag, stoppingToken);
                    logger.LogError("Failed to process message with ChunkId: {ChunkId} delivery tag {DeliveryTag}",
                        consumed.Payload.ChunkId, consumed.DeliveryTag);
                }
                await Task.Delay(3000, stoppingToken); // Simulate some delay
            };
            await messageConsumer.StartConsumingAsync(stoppingToken);
        }
        private async Task ProcessAsync(ConsumedMessage consumedMessage, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var recordingChunkServices = scope.ServiceProvider.GetRequiredService<IRecordingChunkServices>();
                var wavStorageReader = scope.ServiceProvider.GetRequiredService<IWavStorageReader>();
                var transcriptService = scope.ServiceProvider.GetRequiredService<ITranscriptService>();
                var transcriptStorage = scope.ServiceProvider.GetRequiredService<ITranscriptStorage>();

                var chunkId = ChunkId.Of(consumedMessage.Payload.ChunkId);
                var recordingId = RecordingId.Of(consumedMessage.Payload.RecordingId);
                var wavStream = await wavStorageReader.GetWavStreamAsync(chunkId, cancellationToken);
                var result = await transcriptService.TranscribeAsync(wavStream, chunkId, cancellationToken);
                if (result != null) {
                    var storagePath = await transcriptStorage.SaveAsync(result, recordingId, chunkId, cancellationToken);
                    if (!string.IsNullOrEmpty(storagePath)) {
                        var resUpdate = await transcriptStorage.UpdateTranscriptPathAsync(chunkId, storagePath, cancellationToken);
                        if(!resUpdate) throw new InvalidOperationException();
                    }
                    else {
                        throw new InvalidOperationException();
                    }
                }
                else{
                    logger.LogError("Transcription failed for ChunkId: {ChunkId}", chunkId);
                    throw new InvalidOperationException();
                }
            }
            catch
            {
                throw;
            }            
        }
    }
}
