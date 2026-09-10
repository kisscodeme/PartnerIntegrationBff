using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PIBFF.Application.Interfaces;
using PIBFF.Domain.Entities;
using RabbitMQ.Client;
using System.Text.Json;

namespace PIBFF.Infrastructure.Messaging;

/// <summary>RabbitMQ implementation of the application message publisher.</summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqMessagePublisher(
        IConfiguration configuration,
        ILogger<RabbitMqMessagePublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublishAsync(PartnerTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(cancellationToken);
            var queueName = GetSetting("RabbitMq:QueueName", "partner-transactions");
            var body = JsonSerializer.SerializeToUtf8Bytes(transaction, JsonOptions);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = transaction.CorrelationId.ToString(),
                CorrelationId = transaction.CorrelationId.ToString(),
                Type = "PartnerTransaction"
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Published transaction {TransactionReference} to RabbitMQ queue {QueueName} (correlationId: {CorrelationId})",
                transaction.TransactionReference, queueName, transaction.CorrelationId);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            return;

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        var factory = new ConnectionFactory
        {
            HostName = GetSetting("RabbitMq:HostName", "localhost"),
            Port = GetIntSetting("RabbitMq:Port", 5672),
            UserName = GetSetting("RabbitMq:UserName", "guest"),
            Password = GetSetting("RabbitMq:Password", "guest"),
            VirtualHost = GetSetting("RabbitMq:VirtualHost", "/"),
            ClientProvidedName = "PartnerIntegrationBff"
        };

        factory.AutomaticRecoveryEnabled = true;
        factory.TopologyRecoveryEnabled = true;

        _connection = await factory.CreateConnectionAsync(cancellationToken);

        // Publisher confirms make a successful PublishAsync mean that RabbitMQ
        // acknowledged the message, rather than merely that bytes were written
        // to the client socket.
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);

        var queueName = GetSetting("RabbitMq:QueueName", "partner-transactions");
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Connected to RabbitMQ at {Host}:{Port}; queue {QueueName} is ready.",
            factory.HostName, factory.Port, queueName);
    }

    private string GetSetting(string key, string fallback) =>
        _configuration[key] ?? fallback;

    private int GetIntSetting(string key, int fallback) =>
        int.TryParse(_configuration[key], out var value) ? value : fallback;

    public async ValueTask DisposeAsync()
    {
        _publishLock.Dispose();

        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
