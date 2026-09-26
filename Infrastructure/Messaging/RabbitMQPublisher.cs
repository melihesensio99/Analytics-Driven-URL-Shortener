using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Infrastructure.Messaging;

public class RabbitMQPublisher : IMessagePublisher
{
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMQPublisher> _logger;

    public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
    {
        _logger = logger;
        var hostName = configuration["RabbitMQ:Host"] ?? "localhost";
        var port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;

        _factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };
    }

    public async Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _factory.CreateConnectionAsync(cancellationToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Event published to RabbitMQ queue '{QueueName}'", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to RabbitMQ queue '{QueueName}'", queueName);
        }
    }
}
