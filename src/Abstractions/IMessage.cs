namespace Messaging.Core.Abstractions;

/// <summary>
/// Marker contract for all messages flowing through the consumer pipeline.
/// Every broker message must implement this interface to carry traceability metadata.
/// </summary>
public interface IMessage
{
    /// <summary>Unique identifier for this message instance.</summary>
    Guid MessageId { get; }

    /// <summary>UTC timestamp when the message was originally created.</summary>
    DateTimeOffset Timestamp { get; }
}
