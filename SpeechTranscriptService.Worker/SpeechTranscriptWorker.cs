using SpeechTranscriptService.Application.Interfaces;
using SpeechTranscriptService.Domain.Entities;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;

namespace SpeechTranscriptService.Worker
{
    public class SpeechTranscriptWorker(
        IMessageConsumer messageConsumer,
        IMessagePublisher messagePublisher,
        IServiceScopeFactory scopeFactory,
        ILogger<SpeechTranscriptWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            messageConsumer.MessageReceived += async consumed =>
            {
                try
                {
                    await ProcessChunkAsync(consumed, stoppingToken);

                    var recordingId = RecordingId.Of(consumed.Payload.RecordingId);
                    var isRecordingCompleted = await IsRecordingCompletedAsync(recordingId, stoppingToken);
                    if (isRecordingCompleted)
                    {
                        await ConsolidateTranscriptsAsync(recordingId, stoppingToken);
                        logger.LogInformation("Consolidation is ready");
                    }
                    else
                    {
                        logger.LogInformation("Consolidation is not ready");
                    }

                    await messageConsumer.AcknowledgeAsync(consumed.DeliveryTag, stoppingToken);
                }
                catch (Exception ex)
                {
                    await messageConsumer.RejectAsync(consumed.DeliveryTag, stoppingToken);
                    logger.LogError(ex, "Failed to process message with ChunkId: {ChunkId} delivery tag {DeliveryTag}",
                        consumed.Payload.ChunkId, consumed.DeliveryTag);
                }
            };
            await messageConsumer.StartConsumingAsync(stoppingToken);
            //var recordingId = RecordingId.Of(Guid.Parse("6f4bb276-73b4-4572-9f05-5edb28960ae5"));
            //await ConsolidateTranscriptsAsync(recordingId, stoppingToken);
        }
        private async Task<bool> ProcessChunkAsync(ConsumedMessage consumedMessage, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                                
                var wavStorageReader = scope.ServiceProvider.GetRequiredService<IWavStorageReader>();
                var transcriptService = scope.ServiceProvider.GetRequiredService<ITranscriptService>();
                var transcriptStorage = scope.ServiceProvider.GetRequiredService<ITranscriptStorage>();

                var chunkId = ChunkId.Of(consumedMessage.Payload.ChunkId);
                var recordingId = RecordingId.Of(consumedMessage.Payload.RecordingId);
                var wavStream = await wavStorageReader.GetWavStreamAsync(chunkId, cancellationToken);
                if(wavStream is null || wavStream.Length == 0)
                {
                    logger.LogWarning("Can't process the Wav stream for ChunkId: {ChunkId}", chunkId);
                    return false;
                }
                var result = await transcriptService.TranscribeAsync(wavStream, chunkId, cancellationToken);
                if (result != null)
                {
                    var storagePath = await transcriptStorage.SaveAsync(result, recordingId, chunkId, cancellationToken);
                    if (!string.IsNullOrEmpty(storagePath))
                    {
                        var resUpdate = await transcriptStorage.UpdateTranscriptPathAsync(chunkId, storagePath, cancellationToken);
                        if (!resUpdate) throw new InvalidOperationException();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    logger.LogError("Transcription failed for ChunkId: {ChunkId}", chunkId);
                    throw new InvalidOperationException();
                }
                return true;
            }
            catch(Exception ex) 
            {
                logger.LogError(ex, $"Transcription failed for ChunkId: {consumedMessage.Payload.ChunkId}");
                return false;
            }            
        }
        private async Task<bool> IsRecordingCompletedAsync(RecordingId recordingId, CancellationToken cancellationToken)
        {
            using var scope = scopeFactory.CreateScope();
            var recordingChunkServices = scope.ServiceProvider.GetRequiredService<IRecordingChunkServices>();

            return await recordingChunkServices.IsChunksReadyForConsolidationAsync(recordingId, cancellationToken);
        }
        private async Task ConsolidateTranscriptsAsync(RecordingId recordingId, CancellationToken cancellationToken)
        {
            using var scope = scopeFactory.CreateScope();

            var transcriptConsolidator = scope.ServiceProvider.GetRequiredService<ITranscriptConsolidator>();
            var transcriptStorage = scope.ServiceProvider.GetRequiredService<ITranscriptStorage>();
            var recordingService = scope.ServiceProvider.GetRequiredService<IRecordingService>();

            var mergedTranscriptText = await transcriptConsolidator.ConsolidateTranscriptAsync(recordingId, cancellationToken);

            if (!string.IsNullOrEmpty(mergedTranscriptText))
            {
                var destinationPath = $"mergedtranscripts/{DateTime.UtcNow:yyyy-MM-dd}/{recordingId.Value}.json";
                var recordingTranscript = new RecordingTranscript { RecordingId = recordingId, Text = mergedTranscriptText };

                var uploadResponse = await transcriptStorage.UploadRecordingTranscriptAsync(recordingTranscript,
                    destinationPath, cancellationToken);
                if (!uploadResponse)
                {
                    logger.LogWarning("Recording transcripts not stored in the blob for RecordingId {RecordingId}", recordingId);
                    throw new InvalidOperationException();
                }

                var response = await recordingService.UpdateTranscriptPathAsync(recordingId, destinationPath, cancellationToken);
                if (!response)
                {
                    logger.LogWarning("Recording transcript path not updated in DB for RecordingId {RecordingId}", recordingId);
                    throw new InvalidOperationException();
                }

                await messagePublisher.PublishAsync<TranscriptReadyMessage>(
                    new TranscriptReadyMessage(RecordingId: recordingId.Value, CompletedAt: DateTimeOffset.UtcNow),
                    cancellationToken);
            }
        }
    }
}
