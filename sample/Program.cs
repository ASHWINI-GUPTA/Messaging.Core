using Messaging.Core.Extensions;
using Messaging.Core.Pipeline;
using Messaging.Sample.Gates;
using Messaging.Sample.Models;
using Messaging.Sample.Producers;
using Messaging.Sample.Samples;
using Prometheus;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Messaging.Sample host");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    // RabbitMQ Broker
    builder.Services
        .AddRabbitMqBroker(builder.Configuration)
        .AddRabbitMqPublisher();

    // Global pipeline behaviors (apply to ALL consumers)
    builder.Services
        .AddGlobalConsumerBehavior(typeof(LoggingBehavior<>))
        .AddGlobalConsumerBehavior(typeof(TracingBehavior<>));

    // Consumers
    // Pattern: .AddConsumer<TMessage, TConsumer>("exchange", "routingKey")
    //              .WithGate<TGate>()           ← pause if external dependency is down
    //              .WithBehavior<TBehavior>()   ← per-consumer pipeline step
    builder.Services
        .AddConsumer<SampleMessage, SampleConsumer>(
            exchangeName: "sample-exchange",
            routingKey: "sample-queue")
        .WithGate<RandomGate>();

    builder.Services
        .AddConsumer<HardMessage, HardConsumer>(
            exchangeName: "sample-exchange",
            routingKey: "hard-queue")
        .WithGate<SampleGate>();

    // Sample Producer
    // Demonstrate standalone message publishing outside a consumer
    builder.Services.AddHostedService<SampleProducer>();

    // HTTP client for gates
    builder.Services.AddHttpClient("ConsumerGate");

    // Health Checks
    builder.Services.AddConsumerHealthChecks();

    // OpenTelemetry
    builder.Services
        .AddOpenTelemetry()
        .AddConsumerTracing("Messaging.Sample");

    var app = builder.Build();

    // Prometheus scrape endpoint
    app.UseHttpMetrics();
    app.MapMetrics();

    // Health endpoints
    app.MapHealthEndpoint();
    app.MapGet("/", () => Results.Redirect("/health/live"));

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
