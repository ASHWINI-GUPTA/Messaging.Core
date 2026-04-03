using Messaging.Core.Gates;

namespace Messaging.Sample.Gates;

/// <summary>
/// Example gate that blocks consumption when the "Sample External Service" is unavailable.
/// Replace the health endpoint URL and extend this pattern for your real dependencies
/// (e.g., PaymentServiceGate, InventoryServiceGate).
///
/// Registration: <c>.WithGate&lt;SampleGate&gt;()</c> on the consumer builder.
/// </summary>
public sealed class SampleGate(
    IHttpClientFactory httpClientFactory,
    ILogger<SampleGate> logger)
    : HttpDependencyGate("https://status.stripe.com/current", httpClientFactory, logger);
