using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Events;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Messaging;

public class UrlClickConsumerWorker : BackgroundService
{
    private readonly ConnectionFactory _factory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UrlClickConsumerWorker> _logger;
    private const string QueueName = "url-clicks";

    public UrlClickConsumerWorker(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<UrlClickConsumerWorker> logger)
    {
        _serviceProvider = serviceProvider;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 UrlClickConsumerWorker is starting and connecting to RabbitMQ...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = await _factory.CreateConnectionAsync(stoppingToken);
                using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                var batchBuffer = new List<(ulong DeliveryTag, UrlClickLog Log)>();
                var lastFlushTime = DateTime.UtcNow;

                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        var clickEvent = JsonSerializer.Deserialize<UrlClickedEvent>(json);

                        if (clickEvent != null)
                        {
                            var clickLog = new UrlClickLog(
                                clickEvent.ShortCode,
                                clickEvent.IpAddress,
                                clickEvent.UserAgent,
                                clickEvent.Referer,
                                clickEvent.ClickedAt
                            );

                            batchBuffer.Add((ea.DeliveryTag, clickLog));
                        }

                        // Batch Flush Condition: Batch size >= 10 OR TimeSpan >= 3 seconds
                        if (batchBuffer.Count >= 10 || (DateTime.UtcNow - lastFlushTime).TotalSeconds >= 3)
                        {
                            await FlushBatchAsync(channel, batchBuffer, stoppingToken);
                            lastFlushTime = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from RabbitMQ");
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                // Periodic flush check while consumer is idle
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                    if (batchBuffer.Count > 0 && (DateTime.UtcNow - lastFlushTime).TotalSeconds >= 3)
                    {
                        await FlushBatchAsync(channel, batchBuffer, stoppingToken);
                        lastFlushTime = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ Consumer disconnected: {Message}. Retrying in 5 seconds...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task FlushBatchAsync(IChannel channel, List<(ulong DeliveryTag, UrlClickLog Log)> batchBuffer, CancellationToken cancellationToken)
    {
        if (batchBuffer.Count == 0) return;

        var currentBatch = batchBuffer.ToList();
        batchBuffer.Clear();

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var logsToSave = currentBatch.Select(x => x.Log).ToList();
            await dbContext.UrlClickLogs.AddRangeAsync(logsToSave, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Acknowledge all messages in batch
            ulong maxDeliveryTag = currentBatch.Max(x => x.DeliveryTag);
            await channel.BasicAckAsync(maxDeliveryTag, multiple: true, cancellationToken: cancellationToken);

            _logger.LogInformation("✅ BATCH FLUSH SUCCESS: Saved {Count} click logs to PostgreSQL analytics table.", logsToSave.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush batch click logs to database.");
            // Requeue failed batch
            foreach (var item in currentBatch)
            {
                await channel.BasicNackAsync(item.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
            }
        }
    }
}
