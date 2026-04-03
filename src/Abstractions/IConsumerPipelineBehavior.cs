namespace Messaging.Core.Abstractions;

/// <summary>
/// Middleware that wraps every message dispatch for a consumer — similar to ASP.NET ActionFilters
/// but for queue messages. Behaviors run in registration order; each must call <paramref name="next"/>
/// to pass control down the chain to the actual consumer.
///
/// Built-in behaviors: <c>LoggingBehavior</c>, <c>TracingBehavior</c>.
/// Custom behaviors: <c>ValidationBehavior</c>, <c>IdempotencyBehavior</c>, etc.
///
/// Execution order (3 behaviors + consumer):
/// <code>
/// Behavior1.Before → Behavior2.Before → Consumer.ConsumeAsync → Behavior2.After → Behavior1.After
/// </code>
/// </summary>
/// <typeparam name="TMessage">The message type being processed.</typeparam>
public interface IConsumerPipelineBehavior<in TMessage> where TMessage : IMessage
{
    /// <summary>
    /// Handle the message, optionally performing work before and/or after calling <paramref name="next"/>.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="next">Delegate that invokes the next behavior (or the consumer itself).</param>
    /// <param name="cancellationToken">Cancellation token propagated from the host.</param>
    Task HandleAsync(
        TMessage message,
        Func<Task> next,
        CancellationToken cancellationToken);
}
