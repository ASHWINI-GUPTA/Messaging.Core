using Messaging.Core.Abstractions;
using System.Security.Cryptography;

namespace Messaging.Sample.Gates;

/// <summary>
/// Represents a consumer gate that randomly determines whether it is open or closed.
/// </summary>
public sealed class RandomGate : IConsumerGate
{
    public string Name => GetType().Name;

    public Task<bool> IsOpenAsync(CancellationToken cancellationToken)
    {
        bool isOpen = RandomNumberGenerator.GetInt32(0, 2) == 0;
        return Task.FromResult(isOpen);
    }
}