namespace Messaging.Core.Abstractions;

/// <summary>
/// Determines whether a consumer's required external dependencies are available
/// before any messages are pulled from the broker queue.
///
/// When a gate is CLOSED the consumer pauses <c>BasicConsume</c> entirely —
/// messages remain safely queued, no retry budget is spent against a down dependency.
/// The gate is polled on a configurable interval until it reports open, then consumption resumes.
///
/// K8s integration: the readiness probe returns 503 while any gate is closed,
/// preventing new work from being routed to the pod.
/// </summary>
public interface IConsumerGate
{
    /// <summary>
    /// A human-readable name used in log messages and Prometheus gauge labels.
    /// Defaults to the class name if not overridden.
    /// </summary>
    string Name => GetType().Name;

    /// <summary>
    /// Returns <c>true</c> when the dependency is available and the consumer may proceed.
    /// Returns <c>false</c> to pause consumption.
    /// </summary>
    Task<bool> IsOpenAsync(CancellationToken cancellationToken);
}
