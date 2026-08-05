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
    private static readonly CreateChannelOptions PublisherConfirmOptions = new(
    publisherConfirmationsEnabled: true,
    publisherConfirmationTrackingEnabled: true);

    public static async Task<RabbitMqPublisher> CreateAsync(
    IConfiguration config,
    ILogger<RabbitMqPublisher> logger)
    {
        var host = config["RabbitMQ:Host"] ?? throw new InvalidOperationException("Configuration value 'RabbitMQ:Host' is missing.");
        var port = int.Parse(config["RabbitMQ:Port"] ?? "5672");
        var virtualHost = config["RabbitMQ:VirtualHost"];
        virtualHost = string.IsNullOrWhiteSpace(virtualHost) ? "/" : virtualHost;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                VirtualHost = virtualHost,
                UserName = config["RabbitMQ:Username"] ??
                    throw new InvalidOperationException("Configuration value 'RabbitMQ:Username' is missing."),
                Password = config["RabbitMQ:Password"] ??
                    throw new InvalidOperationException("Configuration value 'RabbitMQ:Password' is missing.")
            };

            // CloudAMQP/LavinMQ require TLS on the standard AMQPS port.
            if (port == 5671)
            {
                factory.Ssl = new SslOption { Enabled = true, ServerName = host };
            }

            var connection = await factory.CreateConnectionAsync();

            // Log exactly what we connected to - endpoint, vhost, and the
            // broker's own generated connection name. Compare this against
            // the same log line from the consumer side.
            logger.LogInformation(
                "RabbitMQ connection established. Endpoint: {Endpoint}, VHost: {VHost}, ClientProvidedName: {Name}",
                connection.Endpoint, factory.VirtualHost, connection.ClientProvidedName);

            using (var setupChannel = await connection.CreateChannelAsync())
            {
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
            using var channel = await connection.CreateChannelAsync(
                PublisherConfirmOptions, cancellationToken: ct);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var props = new BasicProperties { Persistent = true };

            // With publisher confirms enabled on this channel, awaiting this
            // call itself blocks until the broker acks - it throws PublishException
            // if the message is nacked or basic.return-ed (e.g. mandatory routing
            // failure), instead of silently succeeding on a fire-and-forget basis.
            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);

            logger.LogInformation(
                "Published {MessageType} to exchange '{Exchange}', broker confirmed. Payload: {@Message}",
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

