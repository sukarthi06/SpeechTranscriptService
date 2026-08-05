using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SpeechTranscriptService.Domain.ValueObjects;
using SpeechTranscriptService.Infra.Interfaces;
using System.Text;
using System.Text.Json;

namespace SpeechTranscriptService.Infra.Services;

public class RabbitMqConsumer(
        IConnection connection,
        IChannel channel,
        ILogger<RabbitMqConsumer> logger) : IMessageConsumer, IAsyncDisposable
{
    // Shared fanout exchange - must match publisher's declaration exactly
    // (type, durable, autoDelete) or RabbitMQ will reject the redeclaration.
    private const string ExchangeName = "chunkwavconversion.completed";

    // This queue belongs to THIS service only. Each independent downstream
    // consumer of the fanout must use its own uniquely-named durable queue -
    // never reuse the exchange name or another service's queue name here,
    // or you'll collide into shared work-queue semantics instead of broadcast.
    private const string QueueName = "chunkwavconversion.completed.speech-transcript-service";

    private string? _consumerTag;

    public event Func<ConsumedMessage, Task>? MessageReceived;

    public static async Task<RabbitMqConsumer> CreateAsync(
        IConfiguration config,
        ILogger<RabbitMqConsumer> logger)
    {
        var host = config["RabbitMQ:Host"] ?? throw new InvalidOperationException("Configuration value 'RabbitMQ:Host' is missing.");
        var port = int.Parse(config["RabbitMQ:Port"] ?? "5672");
        var virtualHost = config["RabbitMQ:VirtualHost"];
        virtualHost = string.IsNullOrWhiteSpace(virtualHost) ? "/" : virtualHost;
        var prefetchCount = ushort.Parse(config["RabbitMQ:PrefetchCount"] ?? "1");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                VirtualHost = virtualHost,
                UserName = config["RabbitMQ:Username"]
                    ?? throw new InvalidOperationException("Configuration value 'RabbitMQ:Username' is missing."),
                Password = config["RabbitMQ:Password"]
                    ?? throw new InvalidOperationException("Configuration value 'RabbitMQ:Password' is missing.")
            };

            // CloudAMQP/LavinMQ require TLS on the standard AMQPS port.
            if (port == 5671)
            {
                factory.Ssl = new SslOption { Enabled = true, ServerName = host };
            }

            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            // Declared defensively here too, in case this consumer starts
            // before the publisher on a fresh broker. Must match the
            // publisher's declaration exactly (fanout, durable, no auto-delete)
            // or the bind below will fail.
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false);

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            // This is the piece that was missing - without a bind, no
            // messages published to the exchange ever reach this queue.
            await channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: string.Empty); // ignored by fanout, but required

            // Prefetch tuned independently of replica count - controls how many
            // unacked messages this consumer holds concurrently.
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: prefetchCount, global: false);

            logger.LogInformation(
                "Connected to RabbitMQ at {Host}:{Port}, bound queue '{Queue}' to exchange '{Exchange}' (prefetch: {Prefetch})",
                host, port, QueueName, ExchangeName, prefetchCount);

            return new RabbitMqConsumer(connection, channel, logger);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Failed to connect to RabbitMQ at {Host}:{Port}. " +
                "Ensure RabbitMQ is running and configuration is correct",
                host, port);
            throw;
        }
    }

    public Task StartConsumingAsync(CancellationToken ct)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            var messageType = nameof(ChunkWavConvertCompletedMessage);

            try
            {
                logger.LogInformation("Received message from queue '{Queue}', delivery tag {Tag}",
                    QueueName, ea.DeliveryTag);

                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var payload = JsonSerializer.Deserialize<ChunkWavConvertCompletedMessage>(json)
                    ?? throw new InvalidOperationException($"Deserialized {messageType} was null.");

                // Hand off and return - this consumer's job stops here.
                // Whoever subscribed decides when (and whether) to ack/reject.
                if (MessageReceived is not null)
                {
                    await MessageReceived.Invoke(new ConsumedMessage(payload, ea.DeliveryTag));
                }
                else
                {
                    logger.LogWarning(
                        "No subscriber for {MessageType} on queue '{Queue}' - message will remain unacked until a subscriber processes it.",
                        messageType, QueueName);
                }
            }
            catch (Exception ex)
            {
                // Only deserialization/framework failures land here - this message
                // can never be processed, so reject it immediately without requeue.
                logger.LogError(ex,
                    "Failed to receive {MessageType} from queue '{Queue}', delivery tag {Tag}. Rejecting without requeue.",
                    messageType, QueueName, ea.DeliveryTag);

                await RejectAsync(ea.DeliveryTag, ct);
            }
        };

        return Task.Run(async () =>
        {
            _consumerTag = await channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: ct);

            logger.LogInformation("Started consuming queue '{Queue}' with tag {Tag}", QueueName, _consumerTag);
        }, ct);
    }

    /// <summary>
    /// Acks a message previously handed off via MessageReceived. Call this once
    /// processing (e.g. blob converted and uploaded) has fully completed - this
    /// is what gives at-least-once delivery semantics.
    /// </summary>
    public async Task AcknowledgeAsync(ulong deliveryTag, CancellationToken ct)
    {
        await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: ct);
        logger.LogInformation("Acked delivery tag {Tag} on queue '{Queue}'", deliveryTag, QueueName);
    }

    /// <summary>
    /// Nacks a message previously handed off via MessageReceived, without requeue,
    /// so a poison message doesn't loop forever. Route to a dead-letter exchange
    /// once one is configured on the queue.
    /// </summary>
    public async Task RejectAsync(ulong deliveryTag, CancellationToken ct)
    {
        await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        logger.LogWarning("Nacked delivery tag {Tag} on queue '{Queue}' (no requeue)", deliveryTag, QueueName);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogInformation("Closing RabbitMQ consumer connection...");

        if (_consumerTag is not null)
        {
            await channel.BasicCancelAsync(_consumerTag);
        }

        await channel.CloseAsync();
        await connection.CloseAsync();

        logger.LogInformation("RabbitMQ consumer connection closed");
    }
}