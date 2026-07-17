namespace SpeechTranscriptService.Infra.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct);
}
