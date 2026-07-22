using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SpeechTranscriptService.Infra.Interfaces;
using System.Text.Json;

namespace SpeechTranscriptService.Infra.Services;

public class RabbitMqPublisher(
        IConnection connection,
        ILogger<RabbitMqPublisher> logger) : IMessagePublisher, IAsyncDisposable
{
    private const string ExchangeName = "transcript.ready";

    public static async Task<RabbitMqPublisher> CreateAsync(
        IConfiguration config,
        ILogger<RabbitMqPublisher> logger)
    {
        var host = config["RabbitMQ:Host"] ?? throw new InvalidOperationException("Configuration value 'RabbitMQ:Host' is missing.");
        var port = int.Parse(config["RabbitMQ:Port"] ?? "5672");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = config["RabbitMQ:Username"] ??
                    throw new InvalidOperationException("Configuration value 'RabbitMQ:Username' is missing."),
                Password = config["RabbitMQ:Password"] ??
                    throw new InvalidOperationException("Configuration value 'RabbitMQ:Password' is missing.")
            };

            var connection = await factory.CreateConnectionAsync();

            using (var setupChannel = await connection.CreateChannelAsync())
            {
                // Fanout ignores routing key entirely - every bound queue gets
                // a copy. Declaring the exchange is this publisher's only
                // setup responsibility; queues are declared and bound by
                // each consuming service independently.
                await setupChannel.ExchangeDeclareAsync(
                    exchange: ExchangeName,
                    type: ExchangeType.Fanout,
                    durable: true,
                    autoDelete: false);
            }

            logger.LogInformation(
                "Connected to RabbitMQ at {Host}:{Port}, exchange '{Exchange}' ready",
                host, port, ExchangeName);

            return new RabbitMqPublisher(connection, logger);
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

    public async Task PublishAsync<T>(T message, CancellationToken ct)
    {
        var messageType = typeof(T).Name;

        try
        {

            using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var props = new BasicProperties { Persistent = true };

            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: string.Empty,   // ignored by fanout exchanges
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);

            logger.LogInformation(
                "Published {MessageType} to exchange '{Exchange}' successfully. Payload: {@Message}",
                messageType, ExchangeName, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish {MessageType} to exchange '{Exchange}'",
                messageType, ExchangeName);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await connection.CloseAsync();
        logger.LogInformation("RabbitMQ connection closed");
    }
}

