namespace Messaging.Core.Abstractions;

/// <summary>
/// Per-publish options applied to the AMQP <c>BasicProperties</c> of each message.
/// Instantiate with <c>new <see cref="MessagePublishOptions"/>()</c> and chain the
/// fluent <c>With*</c> methods to set the desired options.
/// Pass the instance as the optional <c>options</c> argument to
/// <see cref="IMessagePublisher.PublishAsync"/> or
/// <see cref="IMessagePublisher.PublishToQueueAsync"/>.
/// </summary>
public sealed class MessagePublishOptions
{
    private readonly Dictionary<string, object?> _headers = [];

    /// <summary>
    /// Message priority (0–255). Read-only after construction; set via <see cref="WithPriority"/>.
    /// Only honoured by classic queues declared with <c>x-max-priority</c>.
    /// Higher values are delivered to consumers first.
    /// </summary>
    public byte? Priority { get; private set; }

    /// <summary>
    /// Per-message AMQP headers merged into <c>BasicProperties.Headers</c>.
    /// Populated via <see cref="WithHeader"/>. Caller-supplied headers win over
    /// the library's built-in <c>x-message-type</c> and <c>x-published-at</c> on collision.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Headers => _headers;

    /// <summary>
    /// Sets the message priority (0–255).
    /// Priority is only effective on classic queues declared with <c>x-max-priority</c>.
    /// </summary>
    public MessagePublishOptions WithPriority(byte priority)
    {
        Priority = priority;
        return this;
    }

    /// <summary>Adds or overwrites an AMQP message header.</summary>
    public MessagePublishOptions WithHeader(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _headers[key] = value;
        return this;
    }
}
