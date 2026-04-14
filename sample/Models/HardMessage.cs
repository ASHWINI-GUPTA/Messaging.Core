using Messaging.Core.Abstractions;

namespace Messaging.Sample.Models;

/// <summary>
/// Example message demonstrating the recommended record pattern.
/// - <see cref="IMessage.MessageId"/> and <see cref="IMessage.Timestamp"/> are part of the contract.
/// - All other properties are specific to this message type.
/// Replace this with your domain message (e.g., OrderCreatedMessage, LeadScoredMessage).
/// </summary>
public sealed record HardMessage(
    Guid MessageId,
    DateTimeOffset Timestamp,
    string Payload) : IMessage;
