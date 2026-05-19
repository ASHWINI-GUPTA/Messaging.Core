namespace Messaging.Core.RabbitMq;

/// <summary>
/// Fluent configuration object for all RabbitMQ-specific consumer options,
/// covering both queue declaration x-arguments and consumer registration arguments.
/// <para>
/// Configured via the <c>Action&lt;RabbitMqConsumerOptions&gt;</c> delegate passed to
/// <c>ConsumerBuilder.WithRabbitMqOptions</c>. Callers are expected to understand
/// the broker-level implications of each option before enabling it.
/// </para>
/// <para>
/// See: <see href="https://www.rabbitmq.com/docs/queues#optional-arguments"/>
/// </para>
/// </summary>
public sealed class RabbitMqConsumerOptions
{
    private readonly Dictionary<string, object?> _queueArgs = new();

    internal RabbitMqConsumerOptions() { }

    // -------------------------------------------------------------------------
    // Queue declaration x-arguments
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets the maximum number of priority levels for a classic queue (<c>x-max-priority</c>).
    /// Classic queues support priorities in the [0, 255] range.
    /// <para>
    /// RabbitMQ recommends no more than 10 priority levels — each additional level
    /// increases broker memory consumption even when unused.
    /// A <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/> is emitted at
    /// topology setup time when this value exceeds 10.
    /// </para>
    /// </summary>
    public RabbitMqConsumerOptions WithMaxPriority(byte maxPriority)
    {
        _queueArgs["x-max-priority"] = (int)maxPriority;
        return this;
    }

    /// <summary>
    /// Sets the per-message TTL (<c>x-message-ttl</c>).
    /// Messages remaining in the queue longer than this duration are dead-lettered or discarded.
    /// </summary>
    public RabbitMqConsumerOptions WithMessageTtl(TimeSpan ttl)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        _queueArgs["x-message-ttl"] = (long)ttl.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the queue expiry time (<c>x-expires</c>).
    /// The queue is automatically deleted after it has been unused for this duration.
    /// </summary>
    public RabbitMqConsumerOptions WithQueueExpiry(TimeSpan expiry)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry, TimeSpan.Zero);
        _queueArgs["x-expires"] = (long)expiry.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of ready messages the queue will hold (<c>x-max-length</c>).
    /// When the limit is reached the head message is dead-lettered or dropped
    /// depending on the overflow policy.
    /// </summary>
    public RabbitMqConsumerOptions WithMaxLength(int maxMessages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMessages);
        _queueArgs["x-max-length"] = maxMessages;
        return this;
    }

    /// <summary>
    /// Sets the maximum total body size in bytes the queue will hold (<c>x-max-length-bytes</c>).
    /// </summary>
    public RabbitMqConsumerOptions WithMaxLengthBytes(long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        _queueArgs["x-max-length-bytes"] = maxBytes;
        return this;
    }

    /// <summary>
    /// Sets the queue type (<c>x-queue-type</c>): <c>classic</c>, <c>quorum</c>, or <c>stream</c>.
    /// This argument is immutable — changing it after the queue is first declared causes a conflict.
    /// </summary>
    public RabbitMqConsumerOptions WithQueueType(string queueType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueType);
        _queueArgs["x-queue-type"] = queueType;
        return this;
    }

    /// <summary>
    /// Sets an arbitrary x-argument by key and value.
    /// Use this for RabbitMQ arguments not covered by the fluent API.
    /// </summary>
    public RabbitMqConsumerOptions WithArgument(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _queueArgs[key] = value;
        return this;
    }

    // -------------------------------------------------------------------------
    // Consumer registration arguments
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets the <c>x-priority</c> argument passed to <c>BasicConsume</c>.
    /// Higher-priority consumers on the same queue receive messages before lower-priority ones.
    /// </summary>
    public RabbitMqConsumerOptions WithConsumerPriority(byte priority)
    {
        ConsumerPriority = priority;
        return this;
    }

    // -------------------------------------------------------------------------
    // Internal accessors for RabbitMqConsumerService
    // -------------------------------------------------------------------------

    internal IReadOnlyDictionary<string, object?> QueueArguments => _queueArgs;
    internal byte? ConsumerPriority { get; private set; }
    internal byte? MaxPriority =>
        _queueArgs.TryGetValue("x-max-priority", out var v) ? (byte)(int)v! : null;
}
