using Messaging.Core.Abstractions;
using Messaging.Core.Configuration;
using Messaging.Core.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Messaging.Core.RabbitMq;

/// <summary>
/// Core RabbitMQ consumer engine for a single message type.
/// Responsibilities:
///   1. Gate evaluation — pause consumption when external dependencies are unavailable.
///   2. Queue + DLQ topology declaration.
///   3. Message deserialization and consumer pipeline dispatch.
///   4. Retry with exponential back-off (Polly v8).
///   5. Dead-letter routing after max retries.
///   6. Graceful shutdown — drain in-flight messages before closing the channel.
/// </summary>
public sealed class RabbitMqConsumerService<TMessage>(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<ConsumerOptions> options,
    IOptions<RabbitMqOptions> rabbitOptions,
    IEnumerable<IConsumerGate> gates,
    IEnumerable<Type> behaviorTypes,
    ResiliencePipeline retryPipeline,
    ILogger<RabbitMqConsumerService<TMessage>> logger) : IConsumerService, IAsyncDisposable
    where TMessage : IMessage
{
    private readonly ConsumerOptions _options = options.Value;
    private readonly RabbitMqOptions _rabbitOptions = rabbitOptions.Value;
    private readonly IReadOnlyList<IConsumerGate> _gates = gates.ToList();
    private readonly IReadOnlyList<Type> _behaviorTypes = behaviorTypes.ToList();
    private IChannel? _channel;
    private string? _consumerTag;
    private int _inFlightCount;
    private readonly SemaphoreSlim _shutdownSignal = new(0, 1);
    private Task _gateMonitorTask = Task.CompletedTask;
    private CancellationTokenSource? _gateMonitorCts;

    public string QueueName => _options.QueueName;

    /// <summary>
    /// Starts consuming from RabbitMQ, waiting for all gates to open first.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Wait until all gates are open before setting up the channel
        await WaitForGatesAsync(cancellationToken);
        await SetupTopologyAsync(cancellationToken);
        await BeginConsumingAsync(cancellationToken);

        // Start continuous gate monitoring in the background
        if (_gates.Count > 0)
        {
            _gateMonitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _gateMonitorTask = MonitorGatesAsync(_gateMonitorCts.Token);
        }
    }

    /// <summary>
    /// Stops consumption gracefully — waits for in-flight messages to complete
    /// up to <see cref="ConsumerOptions.ShutdownTimeoutSeconds"/> before closing the channel.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        RabbitMqConsumerServiceLog.Stopping(logger, QueueName, _inFlightCount);

        // Stop the background gate monitor
        if (_gateMonitorCts is not null)
        {
            await _gateMonitorCts.CancelAsync();

            // We want to wait for the gate monitor to finish its current loop and exit gracefully, rather than just abandoning it.
            // This allows it to log any final messages about gates being closed during shutdown,
            // and ensures we don't have any background tasks still running that might try to interact with the channel after we've started closing it.
            await _gateMonitorTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            _gateMonitorCts.Dispose();
        }

        if (_channel is not null && _consumerTag is not null)
        {
            await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
            _consumerTag = null;
        }

        // Wait for in-flight messages to complete or timeout
        if (_inFlightCount > 0)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ShutdownTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);

            try
            {
                await _shutdownSignal.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                RabbitMqConsumerServiceLog.ShutdownTimeout(logger, QueueName, _inFlightCount);
            }
        }

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Blocks until all gates are open (called once at startup before consuming begins).
    /// </summary>
    private async Task WaitForGatesAsync(CancellationToken cancellationToken)
    {
        if (_gates.Count == 0) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (await AreAllGatesOpenAsync(cancellationToken))
            {
                RabbitMqConsumerServiceLog.AllGatesOpen(logger, QueueName);
                return;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_options.GatePollingIntervalSeconds),
                cancellationToken);
        }
    }

    /// <summary>
    /// Runs continuously after startup, pausing/resuming the consumer as gates open and close.
    /// </summary>
    private async Task MonitorGatesAsync(CancellationToken cancellationToken)
    {
        // We start in the consuming state, because we won't start the gate monitor until after we've verified all gates are open in WaitForGatesAsync.
        // So at this point we know we're consuming, and if any gate is closed, we need to pause (cancel) the consumer.
        // If we started with consuming: false, then if a gate was closed at this point, we would fail to pause the consumer because we would skip the check for !allOpen && consuming.
        // By starting with consuming: true, we ensure that if any gate is closed at this point, we will correctly pause the consumer.
        var consuming = true; 

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.GatePollingIntervalSeconds),
                    cancellationToken);

                var allOpen = await AreAllGatesOpenAsync(cancellationToken);

                if (!allOpen && consuming)
                {
                    // Gates closed — cancel the consumer to pause delivery
                    if (_consumerTag is not null)
                    {
                        await _channel!.BasicCancelAsync(_consumerTag, cancellationToken: CancellationToken.None);
                        _consumerTag = null;
                    }
                    consuming = false;
                    RabbitMqConsumerServiceLog.GatesPaused(logger, QueueName);
                }
                else if (allOpen && !consuming)
                {
                    // Gates reopened — re-register the consumer to resume delivery
                    await BeginConsumingAsync(CancellationToken.None);
                    consuming = true;
                    RabbitMqConsumerServiceLog.GatesResumed(logger, QueueName);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal on shutdown
                break;
            }
            catch (Exception ex)
            {
                RabbitMqConsumerServiceLog.GateMonitorError(logger, ex, QueueName);
            }
        }
    }

    /// <summary>Evaluates all gates and logs the first closed one.</summary>
    private async Task<bool> AreAllGatesOpenAsync(CancellationToken cancellationToken)
    {
        foreach (var gate in _gates)
        {
            var isOpen = await gate.IsOpenAsync(cancellationToken);
            if (!isOpen)
            {
                RabbitMqConsumerServiceLog.GateClosed(logger, gate.Name, QueueName, _options.GatePollingIntervalSeconds);
                return false;
            }
        }
        return true;
    }

    private async Task SetupTopologyAsync(CancellationToken cancellationToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken);

        // QoS — limits how many messages are delivered to this consumer before ACK
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _options.ConcurrencyLimit,
            global: false,
            cancellationToken: cancellationToken);

        Dictionary<string, object?>? queueArgs = null;

        if (_options.EnableDeadLetterQueue)
        {
            var dlqName = $"{QueueName}.dlq";

            // Declare DLX exchange
            await _channel.ExchangeDeclareAsync(
                exchange: _rabbitOptions.DeadLetterExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            // Declare DLQ bound to DLX
            await _channel.QueueDeclareAsync(
                queue: dlqName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await _channel.QueueBindAsync(
                queue: dlqName,
                exchange: _rabbitOptions.DeadLetterExchangeName,
                routingKey: QueueName,
                cancellationToken: cancellationToken);

            queueArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _rabbitOptions.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = QueueName,
            };

            RabbitMqConsumerServiceLog.DlqConfigured(logger, dlqName);
        }

        // Declare the main queue
        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: cancellationToken);
    }

    private async Task BeginConsumingAsync(CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        _consumerTag = await _channel!.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        RabbitMqConsumerServiceLog.ConsumerStarted(logger, QueueName, _options.ConcurrencyLimit, _options.EnableDeadLetterQueue);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        Interlocked.Increment(ref _inFlightCount);
        var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

        using var activity = ConsumerActivitySource.StartActivity<TMessage>(
            QueueName, messageId, ea.BasicProperties.CorrelationId);

        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["Queue"] = QueueName,
            ["MessageType"] = typeof(TMessage).Name,
        });

        try
        {
            var message = DeserializeMessage(ea.Body);
            if (message is null)
            {
                RabbitMqConsumerServiceLog.DeserializeFailed(logger, messageId);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            await retryPipeline.ExecuteAsync(
                async ct => await DispatchThroughPipelineAsync(message, ct),
                CancellationToken.None);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            activity?.SetStatus(ActivityStatusCode.Ok);

            RabbitMqConsumerServiceLog.MessageProcessed(logger, messageId, typeof(TMessage).Name);
        }
        catch (Exception ex)
        {
            RabbitMqConsumerServiceLog.MessageFailed(logger, ex, messageId, typeof(TMessage).Name);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Nack without requeue — DLX will catch it if configured
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
        finally
        {
            if (Interlocked.Decrement(ref _inFlightCount) == 0 && _consumerTag is null)
            {
                // Signal graceful shutdown that all in-flight messages are done
                _shutdownSignal.Release();
            }
        }
    }

    private async Task DispatchThroughPipelineAsync(TMessage message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // Build the pipeline chain: outermost behavior wraps innermost
        Func<Task> pipeline = () => InvokeConsumerAsync(scope, message, cancellationToken);

        foreach (var behaviorType in Enumerable.Reverse(_behaviorTypes))
        {
            var behavior = (IConsumerPipelineBehavior<TMessage>)scope.ServiceProvider
                .GetRequiredService(behaviorType);

            var next = pipeline;
            pipeline = () => behavior.HandleAsync(message, next, cancellationToken);
        }

        await pipeline();
    }

    private static async Task InvokeConsumerAsync(
        AsyncServiceScope scope, TMessage message, CancellationToken cancellationToken)
    {
        var consumer = scope.ServiceProvider.GetRequiredService<IMessageConsumer<TMessage>>();
        await consumer.ConsumeAsync(message, cancellationToken);
    }

    private static TMessage? DeserializeMessage(ReadOnlyMemory<byte> body)
    {
        try
        {
            var json = Encoding.UTF8.GetString(body.Span);
            return JsonSerializer.Deserialize<TMessage>(json);
        }
        catch
        {
            return default;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _shutdownSignal.Dispose();
        _gateMonitorCts?.Dispose();
    }
}

/// <summary>High-performance logger messages for <see cref="RabbitMqConsumerService{TMessage}"/>.</summary>
internal static partial class RabbitMqConsumerServiceLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Stopping consumer for {Queue}. In-flight messages: {Count}")]
    internal static partial void Stopping(ILogger logger, string queue, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Shutdown timeout reached for {Queue}. {Count} messages may be incomplete.")]
    internal static partial void ShutdownTimeout(ILogger logger, string queue, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Consumer gate {GateName} is CLOSED for queue {Queue}. Consumption paused. Retrying in {IntervalSeconds}s.")]
    internal static partial void GateClosed(ILogger logger, string gateName, string queue, int intervalSeconds);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "All consumer gates are OPEN for queue {Queue}. Starting consumption.")]
    internal static partial void AllGatesOpen(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Consumer gates CLOSED for queue {Queue}. Pausing message delivery.")]
    internal static partial void GatesPaused(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Consumer gates REOPENED for queue {Queue}. Resuming message delivery.")]
    internal static partial void GatesResumed(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Unhandled error in gate monitor for queue {Queue}. Will retry.")]
    internal static partial void GateMonitorError(ILogger logger, Exception ex, string queue);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "DLQ topology configured: {DlqName}")]
    internal static partial void DlqConfigured(ILogger logger, string dlqName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Consumer started on queue {Queue} (prefetch={Prefetch}, DLQ={Dlq})")]
    internal static partial void ConsumerStarted(ILogger logger, string queue, ushort prefetch, bool dlq);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to deserialize message {MessageId}. Sending to DLQ.")]
    internal static partial void DeserializeFailed(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Message {MessageId} ({MessageType}) processed successfully")]
    internal static partial void MessageProcessed(ILogger logger, string messageId, string messageType);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Message {MessageId} ({MessageType}) failed after all retries. Routing to DLQ.")]
    internal static partial void MessageFailed(ILogger logger, Exception ex, string messageId, string messageType);
}
