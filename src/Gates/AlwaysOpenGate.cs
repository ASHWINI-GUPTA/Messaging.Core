using Messaging.Core.Abstractions;

namespace Messaging.Core.Gates;

/// <summary>
/// Default no-op gate — always open.
/// Used automatically when no gate is registered for a consumer.
/// </summary>
public sealed class AlwaysOpenGate : IConsumerGate
{
    public string Name => "AlwaysOpen";

    public Task<bool> IsOpenAsync(CancellationToken cancellationToken)
        => Task.FromResult(true);
}
