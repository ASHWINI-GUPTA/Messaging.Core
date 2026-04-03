namespace Messaging.Core.Abstractions;

/// <summary>
/// Core contract for typed message consumers.
/// Implement this interface for each message type your service needs to handle.
/// Consumers are resolved from a DI scope per message — they may be Scoped or Transient.
/// </summary>
/// <typeparam name="TMessage">The message type this consumer handles.</typeparam>
public interface IMessageConsumer<in TMessage> where TMessage : IMessage
{
    /// <summary>
    /// Handle a single incoming message.
    /// Throw any exception to trigger the retry/DLQ policy.
    /// </summary>
    Task ConsumeAsync(TMessage message, CancellationToken cancellationToken);
}
