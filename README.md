# Berberis CrossBar Messaging

[![CI](https://github.com/azixaka/Berberis/actions/workflows/ci.yml/badge.svg)](https://github.com/azixaka/Berberis/actions/workflows/ci.yml)
[![Nuget](https://img.shields.io/nuget/v/Berberis.Messaging)](https://www.nuget.org/packages/Berberis.Messaging/)

Berberis CrossBar is a high-performance, allocation-free in-process message broker designed for creating complex, high-speed pipelines within a single process. Built on the concept of typed channels, Berberis CrossBar serves as the bridge connecting publishers and subscribers within your application.

## Features

- **Typed Channels**: Each channel in Berberis CrossBar is a typed destination, creating a clear and efficient path for message passing between publishers and subscribers.

- **Message Conflation**: Berberis CrossBar supports message conflation, enhancing the efficiency of your messaging system by preventing the overloading of channels with redundant or unnecessary data.

- **Record/Replay**: The broker provides a record/replay feature that can serialize a stream of updates into a stream. This serialized stream can then be deserialized and published on a channel, facilitating efficient data replay and debugging.

- **Comprehensive Observability**: With Berberis CrossBar, you can trace not only messages but also a wide array of statistics, including service time, latencies, rates, sources, percentiles, and more. This empowers you to gain deeper insights into the performance of your messaging system and make data-driven optimizations.

- **Stateful Channels**: Berberis CrossBar offers stateful channels, which store the latest published messages by key. This allows new subscribers to fetch the most recent state of the channel upon subscription, keeping everyone up-to-date and in sync.

- **Channel Reset and Message Deletions**: Berberis CrossBar also supports channel resets, allowing you to clear a channel and start fresh when necessary. Individual message deletions are also supported, ensuring that you have full control over the data in your channels.

- **Wildcard Subscriptions**: The system supports wildcard subscription patterns like '*' and '>', offering you more flexibility and control over the messages you subscribe to.

- **Metrics Export**: With the MetricsToJson extension method, you can easily generate a comprehensive JSON report of metrics from all CrossBar channels and each subscription. This feature provides an efficient way to monitor and optimize the performance of your messaging system.

## Getting Started

You can add Berberis CrossBar to your project through NuGet:

```sh
Install-Package Berberis.Messaging
```

## Quick Start

### Basic Publish/Subscribe

Here's a simple example of publishing and subscribing to messages:

```csharp
using Berberis.Messaging;

// Create the CrossBar instance
ICrossBar xBar = new CrossBar();

// Define a simple message type
public record StockPrice(string Symbol, double Price);

// Subscribe to a channel
using var subscription = xBar.Subscribe<StockPrice>("stock.prices",
    async msg =>
    {
        Console.WriteLine($"Received: {msg.Body.Symbol} @ ${msg.Body.Price:N2}");
        return ValueTask.CompletedTask;
    });

// Publish messages
await xBar.Publish("stock.prices", new StockPrice("AAPL", 150.25));
await xBar.Publish("stock.prices", new StockPrice("GOOGL", 2800.50));

// Wait for all messages to be processed
await subscription.MessageLoop;
```

### Understanding the Message Envelope

Every message in Berberis is wrapped in a `Message<TBody>` envelope that provides metadata:

```csharp
xBar.Subscribe<StockPrice>("stock.prices", msg =>
{
    Console.WriteLine($"Message ID: {msg.Id}");
    Console.WriteLine($"Timestamp: {DateTime.FromBinary(msg.Timestamp):o}");
    Console.WriteLine($"Type: {msg.MessageType}");
    Console.WriteLine($"From: {msg.From}");
    Console.WriteLine($"Key: {msg.Key}");
    Console.WriteLine($"Correlation ID: {msg.CorrelationId}");
    Console.WriteLine($"Body: {msg.Body}");

    return ValueTask.CompletedTask;
});
```

### Publishing with Metadata

You can publish messages with rich metadata for routing, correlation, and state management:

```csharp
var correlationId = xBar.GetNextCorrelationId();

await xBar.Publish(
    channel: "stock.prices",
    body: new StockPrice("MSFT", 310.75),
    correlationId: correlationId,
    key: "MSFT",           // Used for stateful channels and conflation
    store: true,           // Store in channel state
    from: "MarketDataService"  // Source identifier
);
```

## Advanced Features

### Stateful Channels

Stateful channels store the latest message for each unique key. New subscribers can fetch the entire state when they connect:

```csharp
// Producer: Publish messages with keys and store=true
public class StockPriceProducer : BackgroundService
{
    private readonly ICrossBar _xBar;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN" };
        var random = new Random();

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var symbol in symbols)
            {
                var price = new StockPrice(symbol, random.NextDouble() * 1000);

                // Store the latest price for each symbol
                await _xBar.Publish(
                    channel: "stock.prices",
                    body: price,
                    key: symbol,      // Key for state storage
                    store: true       // Enable state storage
                );
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}

// Consumer: Fetch existing state on subscription
public class StockPriceConsumer : BackgroundService
{
    private readonly ICrossBar _xBar;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = _xBar.Subscribe<StockPrice>(
            channelName: "stock.prices",
            handler: msg =>
            {
                Console.WriteLine($"{msg.Body.Symbol}: ${msg.Body.Price:N2}");
                return ValueTask.CompletedTask;
            },
            fetchState: true  // Receive all stored messages first
        );

        await subscription.MessageLoop;
    }
}

// Manually query channel state
var currentPrices = xBar.GetChannelState<StockPrice>("stock.prices");
foreach (var msg in currentPrices)
{
    Console.WriteLine($"{msg.Key}: {msg.Body}");
}

// Delete a specific message by key
if (xBar.TryDeleteMessage<StockPrice>("stock.prices", "AAPL", out var deleted))
{
    Console.WriteLine($"Deleted price for {deleted.Body.Symbol}");
}

// Reset entire channel (clear all state)
xBar.ResetChannel<StockPrice>("stock.prices");
```

### Message Conflation

Conflation reduces message volume by deduplicating updates to the same key within a time window. Only the latest message per key is delivered:

```csharp
// Without conflation: Receive every single update (could be thousands per second)
using var fastSubscription = xBar.Subscribe<StockPrice>(
    "stock.prices",
    handler: msg => ProcessPrice(msg)
);

// With conflation: Receive at most one update per key per second
using var conflatedSubscription = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices",
    handler: msg => ProcessPrice(msg),
    fetchState: false,
    conflationInterval: TimeSpan.FromSeconds(1)  // Conflate updates over 1 second
);

// Conflation with state fetching
using var subscription = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices",
    handler: msg => ProcessPrice(msg),
    fetchState: true,                            // Get existing state first
    conflationInterval: TimeSpan.FromMilliseconds(500)  // Then conflate live updates
);
```

**When to use conflation:**
- High-frequency updates where you only care about the latest value
- UI updates that don't need every intermediate state
- Downstream systems that can't keep up with full message rates
- Reducing processing overhead while maintaining data freshness

### Wildcard Subscriptions

Subscribe to multiple channels using wildcard patterns:

- `*` matches exactly one segment
- `>` matches all remaining segments

```csharp
// Subscribe to all stock prices for a specific exchange
// Matches: "stock.prices.NYSE", "stock.prices.NASDAQ", etc.
using var singleWildcard = xBar.Subscribe<StockPrice>(
    "stock.prices.*",
    msg => Console.WriteLine($"Exchange price: {msg.Body}")
);

// Subscribe to everything under "stock"
// Matches: "stock.prices", "stock.prices.NYSE", "stock.trades", "stock.orders.limit", etc.
using var multiWildcard = xBar.Subscribe<object>(
    "stock.>",
    msg => Console.WriteLine($"Stock event: {msg.Body}")
);

// More specific patterns
using var specificWildcard = xBar.Subscribe<Trade>(
    "stock.trades.*.limit",  // Matches "stock.trades.NYSE.limit", "stock.trades.NASDAQ.limit"
    msg => ProcessLimitTrade(msg)
);

// Wildcard with state fetching and conflation
using var subscription = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices.>",
    handler: msg => ProcessPrice(msg),
    name: "AllStockPricesSubscription",  // Named subscription for observability
    fetchState: true,
    conflationInterval: TimeSpan.FromMilliseconds(100)
);

await subscription.MessageLoop;
```

### Record and Replay

Capture message streams to binary files for debugging, testing, or auditing:

```csharp
using Berberis.Recorder;

// Define a serializer for your message type
public class StockPriceSerializer : IBinaryCodec<StockPrice>
{
    public void Encode(BinaryWriter writer, StockPrice value)
    {
        writer.Write(value.Symbol);
        writer.Write(value.Price);
    }

    public StockPrice Decode(BinaryReader reader)
    {
        var symbol = reader.ReadString();
        var price = reader.ReadDouble();
        return new StockPrice(symbol, price);
    }
}

// Recording: Capture messages to a file
public class RecorderService : BackgroundService
{
    private readonly ICrossBar _xBar;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var fileStream = File.OpenWrite("stock-prices.bin");
        using var recording = _xBar.Record(
            channelOrPattern: "stock.prices.>",
            stream: fileStream,
            codec: new StockPriceSerializer(),
            cancellationToken: stoppingToken
        );

        // Record for 10 seconds
        await Task.Delay(10_000, stoppingToken);

        // Dispose stops the recording
        recording.Dispose();
        await recording.MessageLoop;
    }
}

// Playback: Replay recorded messages
public class PlayerService : BackgroundService
{
    private readonly ICrossBar _xBar;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var fileStream = File.OpenRead("stock-prices.bin");
        var player = Player<StockPrice>.Create(fileStream, new StockPriceSerializer());

        await foreach (var message in player.MessagesAsync(stoppingToken))
        {
            // Republish to the same or different channel
            await _xBar.Publish("replay.stock.prices", message.Body, key: message.Key);

            // Optional: Simulate original timing
            // await Task.Delay(originalInterval);
        }
    }
}
```

### Observability and Metrics

Berberis provides comprehensive performance metrics for every channel and subscription:

```csharp
using Berberis.Messaging.Statistics;
using System.Text.Json;

// Subscribe with custom statistics tracking
var statsOptions = new StatsOptions(
    percentile: 0.99f,      // Track 99th percentile latency
    alpha: 0.015f,          // Moving percentile smoothing
    delta: 0.01f,           // Moving percentile delta
    ewmaWindowSize: 100     // EWMA window for rates
);

using var subscription = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices",
    handler: msg => ProcessPrice(msg),
    name: "PriceProcessor",
    statsOptions: statsOptions
);

// Access subscription statistics
var stats = subscription.Statistics.GetStats(reset: false);
Console.WriteLine($"Dequeue Rate: {stats.DequeueRate:F2} msg/s");
Console.WriteLine($"Process Rate: {stats.ProcessRate:F2} msg/s");
Console.WriteLine($"Avg Latency: {stats.AvgLatencyTimeMs:F4} ms");
Console.WriteLine($"P99 Latency: {stats.PercentileLatencyTimeMs:F4} ms");
Console.WriteLine($"Avg Service Time: {stats.AvgServiceTimeMs:F4} ms");
Console.WriteLine($"Queue Length: {stats.QueueLength}");
Console.WriteLine($"Conflation Ratio: {stats.ConflationRatio:F4}");

// Export all metrics to JSON
using var memoryStream = new MemoryStream();
using var writer = new Utf8JsonWriter(memoryStream);

xBar.MetricsToJson(
    writer: writer,
    useMnemonics: true,  // Use short metric names
    resetStats: false    // Keep stats after export
);

writer.Flush();
var json = Encoding.UTF8.GetString(memoryStream.ToArray());
Console.WriteLine(json);

// Monitor all channels and subscriptions
foreach (var channel in xBar.GetChannels())
{
    var channelStats = channel.Statistics.GetStats(reset: false);
    Console.WriteLine($"\nChannel: {channel.Name} ({channel.BodyType.Name})");
    Console.WriteLine($"  Publish Rate: {channelStats.PublishRate:F2} msg/s");
    Console.WriteLine($"  Last Published By: {channel.LastPublishedBy}");

    foreach (var sub in xBar.GetChannelSubscriptions(channel.Name))
    {
        var subStats = sub.Statistics.GetStats(reset: false);
        Console.WriteLine($"  Subscription: {sub.Name}");
        Console.WriteLine($"    Process Rate: {subStats.ProcessRate:F2} msg/s");
        Console.WriteLine($"    Avg Latency: {subStats.AvgLatencyTimeMs:F4} ms");
    }
}

// Enable message tracing to system channel
xBar.MessageTracingEnabled = true;

using var tracingSubscription = xBar.Subscribe<MessageTrace>(
    "$message.traces",  // System channel for traces
    msg =>
    {
        Console.WriteLine($"Trace: {msg.Body}");
        return ValueTask.CompletedTask;
    }
);
```

#### Exporting Metrics to JSON

The `MetricsToJson` extension method provides a comprehensive JSON export of all channel and subscription metrics. This is ideal for:
- Monitoring dashboards
- Log aggregation systems
- Performance analysis tools
- Real-time health checks

```csharp
using System.Text.Json;

// Export metrics to JSON
using var memoryStream = new MemoryStream();
using var writer = new Utf8JsonWriter(memoryStream, new JsonWriterOptions
{
    Indented = true  // Pretty-print for readability
});

xBar.MetricsToJson(
    writer: writer,
    useMnemonics: false,  // Use full property names
    resetStats: false     // Keep statistics after export
);

writer.Flush();
var json = Encoding.UTF8.GetString(memoryStream.ToArray());
Console.WriteLine(json);

// Write directly to a file
using var fileStream = File.Create("crossbar-metrics.json");
using var fileWriter = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = true });
xBar.MetricsToJson(fileWriter, useMnemonics: false, resetStats: false);

// Use mnemonics for compact JSON (smaller payload)
xBar.MetricsToJson(writer, useMnemonics: true, resetStats: false);
```

**Example JSON Output (Full Format):**

```json
{
  "Channels": [
    {
      "Channel": "stock.prices",
      "MessageBodyType": "MyApp.StockPrice",
      "LastPublishedBy": "StockPriceProducerService",
      "LastPublishedAt": "23/10/2025 14:30:45.123",
      "IntervalMs": 1000.50,
      "PublishRate": 1250.75,
      "TotalMessages": 125000
    },
    {
      "Channel": "stock.trades",
      "MessageBodyType": "MyApp.Trade",
      "LastPublishedBy": "TradeExecutor",
      "LastPublishedAt": "23/10/2025 14:30:45.098",
      "IntervalMs": 1000.25,
      "PublishRate": 450.20,
      "TotalMessages": 45000
    }
  ],
  "Subscriptions": [
    {
      "Name": "PriceProcessor_MainSubscription",
      "SubscribedAt": "23/10/2025 14:25:10.000",
      "Subscriptions": [
        "stock.prices"
      ],
      "ConflationInterval": "00:00:00.5000000",
      "ConflationRatio": 0.3456,
      "LatencyToResponseTimeRatio": 0.0125,
      "IntervalMs": 1000.50,
      "DequeueRate": 1248.25,
      "ProcessRate": 1248.20,
      "EstimatedAvgActiveMessages": 1.25,
      "TotalEnqueuedMessages": 125000,
      "TotalDequeuedMessages": 124980,
      "TotalProcessedMessages": 124950,
      "QueueLength": 50,
      "AvgLatencyTimeMs": 0.1234,
      "MinLatencyTimeMs": 0.0450,
      "MaxLatencyTimeMs": 2.3500,
      "AvgServiceTimeMs": 1.2500,
      "MinServiceTimeMs": 0.8000,
      "MaxServiceTimeMs": 5.6700,
      "AvgResponseTimeMs": 1.3750,
      "StatsPercentile": 99,
      "PctLatencyTimeMs": 0.8900,
      "PctServiceTimeMs": 3.2100
    },
    {
      "Name": "WildcardTradeMonitor",
      "SubscribedAt": "23/10/2025 14:25:12.500",
      "Subscriptions": [
        "stock.trades",
        "stock.trades.NYSE",
        "stock.trades.NASDAQ"
      ],
      "Expression": "stock.trades.>",
      "LatencyToResponseTimeRatio": 0.0089,
      "IntervalMs": 1000.25,
      "DequeueRate": 450.15,
      "ProcessRate": 450.15,
      "EstimatedAvgActiveMessages": 0.5,
      "TotalEnqueuedMessages": 45000,
      "TotalDequeuedMessages": 45000,
      "TotalProcessedMessages": 45000,
      "QueueLength": 0,
      "AvgLatencyTimeMs": 0.0890,
      "MinLatencyTimeMs": 0.0200,
      "MaxLatencyTimeMs": 1.5000,
      "AvgServiceTimeMs": 0.5600,
      "MinServiceTimeMs": 0.2000,
      "MaxServiceTimeMs": 2.3000,
      "AvgResponseTimeMs": 0.6490
    }
  ]
}
```

**Example JSON Output (Mnemonic Format):**

The mnemonic format uses shortened property names to reduce JSON payload size, ideal for high-frequency monitoring or bandwidth-constrained scenarios:

```json
{
  "Chs": [
    {
      "Ch": "stock.prices",
      "Tp": "MyApp.StockPrice",
      "PubBy": "StockPriceProducerService",
      "PubAt": "23/10/2025 14:30:45.123",
      "InMs": 1000.50,
      "Rt": 1250.75,
      "TMsg": 125000
    }
  ],
  "Sbs": [
    {
      "Nm": "PriceProcessor_MainSubscription",
      "SubAt": "23/10/2025 14:25:10.000",
      "Sbs": ["stock.prices"],
      "CfIn": "00:00:00.5000000",
      "CfRat": 0.3456,
      "LatRsp": 0.0125,
      "InMs": 1000.50,
      "DqRt": 1248.25,
      "PcRt": 1248.20,
      "EstAvgAMsg": 1.25,
      "TEqMsg": 125000,
      "TDqMsg": 124980,
      "TPcMsg": 124950,
      "QLn": 50,
      "AvgLat": 0.1234,
      "MinLat": 0.0450,
      "MaxLat": 2.3500,
      "AvgSvc": 1.2500,
      "MinSvc": 0.8000,
      "MaxSvc": 5.6700,
      "AvgRsp": 1.3750,
      "StPct": 99,
      "PctLat": 0.8900,
      "PctSvc": 3.2100
    }
  ]
}
```

**Metric Definitions:**

**Channel Metrics:**
- `Channel` / `Ch`: Channel name
- `MessageBodyType` / `Tp`: Fully qualified type name of message body
- `LastPublishedBy` / `PubBy`: Source identifier of last publisher
- `LastPublishedAt` / `PubAt`: Timestamp of last publish (dd/MM/yyyy HH:mm:ss.fff)
- `IntervalMs` / `InMs`: Measurement interval in milliseconds
- `PublishRate` / `Rt`: Messages published per second
- `TotalMessages` / `TMsg`: Total messages published to channel

**Subscription Metrics:**
- `Name` / `Nm`: Subscription name
- `SubscribedAt` / `SubAt`: Subscription creation time
- `Subscriptions` / `Sbs`: Array of channel names this subscription receives from
- `Expression` / `Exp`: Wildcard pattern (only present for wildcard subscriptions)
- `ConflationInterval` / `CfIn`: Conflation time window (only if conflation enabled)
- `ConflationRatio` / `CfRat`: Ratio of messages conflated (0-1, only if conflation enabled)
- `LatencyToResponseTimeRatio` / `LatRsp`: Latency/response time ratio (lower = more efficient)
- `IntervalMs` / `InMs`: Measurement interval in milliseconds
- `DequeueRate` / `DqRt`: Messages dequeued per second
- `ProcessRate` / `PcRt`: Messages processed per second
- `EstimatedAvgActiveMessages` / `EstAvgAMsg`: Average messages being processed concurrently
- `TotalEnqueuedMessages` / `TEqMsg`: Total messages received
- `TotalDequeuedMessages` / `TDqMsg`: Total messages dequeued for processing
- `TotalProcessedMessages` / `TPcMsg`: Total messages successfully processed
- `QueueLength` / `QLn`: Current queue depth
- `AvgLatencyTimeMs` / `AvgLat`: Average time from publish to dequeue
- `MinLatencyTimeMs` / `MinLat`: Minimum latency observed
- `MaxLatencyTimeMs` / `MaxLat`: Maximum latency observed
- `AvgServiceTimeMs` / `AvgSvc`: Average handler execution time
- `MinServiceTimeMs` / `MinSvc`: Minimum service time observed
- `MaxServiceTimeMs` / `MaxSvc`: Maximum service time observed
- `AvgResponseTimeMs` / `AvgRsp`: Average total time (latency + service time)
- `StatsPercentile` / `StPct`: Configured percentile (e.g., 99 for P99)
- `PctLatencyTimeMs` / `PctLat`: Percentile latency time
- `PctServiceTimeMs` / `PctSvc`: Percentile service time

**Real-World Integration Examples:**

```csharp
// 1. Periodic export to monitoring system
public class MetricsExporter : BackgroundService
{
    private readonly ICrossBar _xBar;
    private readonly IHttpClientFactory _httpClientFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        while (!ct.IsCancellationRequested)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            _xBar.MetricsToJson(writer, useMnemonics: true, resetStats: true);
            writer.Flush();

            // Send to monitoring endpoint
            var content = new ByteArrayContent(stream.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            await httpClient.PostAsync("https://monitoring.example.com/metrics", content, ct);

            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }
}

// 2. Health check endpoint in ASP.NET Core
app.MapGet("/health/crossbar", (ICrossBar xBar) =>
{
    using var stream = new MemoryStream();
    using var writer = new Utf8JsonWriter(stream);

    xBar.MetricsToJson(writer, useMnemonics: false, resetStats: false);
    writer.Flush();

    return Results.Json(
        JsonDocument.Parse(stream.ToArray()).RootElement,
        statusCode: 200
    );
});

// 3. Alert on metrics thresholds
public async Task CheckHealthMetrics(ICrossBar xBar)
{
    using var stream = new MemoryStream();
    using var writer = new Utf8JsonWriter(stream);

    xBar.MetricsToJson(writer, useMnemonics: false, resetStats: false);
    writer.Flush();

    var metrics = JsonDocument.Parse(stream.ToArray());
    var subscriptions = metrics.RootElement.GetProperty("Subscriptions");

    foreach (var sub in subscriptions.EnumerateArray())
    {
        var queueLength = sub.GetProperty("QueueLength").GetInt64();
        var avgLatency = sub.GetProperty("AvgLatencyTimeMs").GetDouble();
        var name = sub.GetProperty("Name").GetString();

        if (queueLength > 10000)
            _logger.LogWarning("High queue depth on {Name}: {QueueLength}", name, queueLength);

        if (avgLatency > 100)
            _logger.LogWarning("High latency on {Name}: {Latency}ms", name, avgLatency);
    }
}
```

### Error Handling and Backpressure

Control how subscriptions handle slow consumers and errors:

```csharp
using Berberis.Messaging;

// Handler timeouts: Prevent slow handlers from blocking
var options = new SubscriptionOptions
{
    HandlerTimeout = TimeSpan.FromSeconds(5),
    OnTimeout = ex =>
    {
        Console.WriteLine($"Handler timed out for message {ex.MessageId}");
        // Log, alert, or take corrective action
    }
};

using var subscription = xBar.Subscribe<StockPrice>(
    "stock.prices",
    async msg =>
    {
        // This handler must complete within 5 seconds
        await ProcessPriceAsync(msg.Body);
        return ValueTask.CompletedTask;
    },
    options: options
);

// Slow consumer strategies: What happens when the buffer fills up
using var skipUpdates = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices",
    handler: msg => SlowProcess(msg),
    slowConsumerStrategy: SlowConsumerStrategy.SkipUpdates  // Default: Skip new messages
);

using var failSubscription = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices",
    handler: msg => SlowProcess(msg),
    slowConsumerStrategy: SlowConsumerStrategy.FailSubscription  // Throw FailedSubscriptionException
);

using var conflateAndSkip = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices",
    handler: msg => SlowProcess(msg),
    slowConsumerStrategy: SlowConsumerStrategy.ConflateAndSkipUpdates  // Conflate keyed messages
);

// Handle subscription failures
try
{
    using var sub = xBar.Subscribe<StockPrice>(
        "stock.prices",
        handler: msg => VerySlowProcess(msg),
        slowConsumerStrategy: SlowConsumerStrategy.FailSubscription
    );

    await sub.MessageLoop;
}
catch (FailedSubscriptionException ex)
{
    Console.WriteLine($"Subscription failed: {ex.Message}");
    Console.WriteLine($"Channel: {ex.ChannelName}");
    // Implement retry logic, alerting, etc.
}

// Pause and resume processing
using var pausableSubscription = xBar.Subscribe<StockPrice>(
    "stock.prices",
    msg => ProcessPrice(msg)
);

// Temporarily suspend processing (messages still enqueued)
pausableSubscription.IsProcessingSuspended = true;
await Task.Delay(5000);
pausableSubscription.IsProcessingSuspended = false;

// Detach subscription (stops receiving new messages)
pausableSubscription.IsDetached = true;
await Task.Delay(5000);
pausableSubscription.IsDetached = false;

// Monitor timeout counts
var timeoutCount = subscription.GetTimeoutCount();
if (timeoutCount > 100)
{
    Console.WriteLine($"Warning: {timeoutCount} handler timeouts detected");
}
```

## Performance Tips and Best Practices

### 1. Use ValueTask for Synchronous Handlers

```csharp
// Good: Zero allocation for synchronous completion
xBar.Subscribe<StockPrice>("prices", msg =>
{
    ProcessSync(msg.Body);
    return ValueTask.CompletedTask;  // No heap allocation
});

// Avoid: Creates Task allocation even for sync work
xBar.Subscribe<StockPrice>("prices", async msg =>
{
    ProcessSync(msg.Body);  // Not actually async
    await Task.CompletedTask;
});
```

### 2. Enable Conflation for High-Frequency Updates

```csharp
// High-frequency source: 10,000 updates/sec
// Consumer only needs latest value

using var subscription = xBar.Subscribe<StockPrice>(
    "hft.prices",
    msg => UpdateUI(msg.Body),
    conflationInterval: TimeSpan.FromMilliseconds(100)  // Max 10 updates/sec
);
```

### 3. Use Stateful Channels for Late Joiners

```csharp
// Ensure new subscribers get current state
await xBar.Publish("reference.data", data, key: data.Id, store: true);

// Late subscribers automatically get all stored messages
using var lateSubscription = xBar.Subscribe<ReferenceData>(
    "reference.data",
    msg => LoadReference(msg.Body),
    fetchState: true
);
```

### 4. Leverage Wildcard Subscriptions for Routing

```csharp
// Better: Single wildcard subscription
using var allStocks = xBar.Subscribe<StockPrice>(
    "stock.prices.*",
    msg => ProcessAnyStock(msg.Body)
);

// Avoid: Multiple individual subscriptions
// using var aapl = xBar.Subscribe<StockPrice>("stock.prices.AAPL", ...);
// using var googl = xBar.Subscribe<StockPrice>("stock.prices.GOOGL", ...);
// ... (hundreds more)
```

### 5. Name Your Subscriptions for Observability

```csharp
using var subscription = xBar.Subscribe<StockPrice>(
    channelName: "stock.prices",
    handler: msg => Process(msg),
    name: "RiskCalculator_PriceSubscription",  // Clear, descriptive name
    statsOptions: new StatsOptions(percentile: 0.99f)
);

// Makes metrics and debugging much easier
foreach (var sub in xBar.GetChannelSubscriptions("stock.prices"))
{
    Console.WriteLine($"{sub.Name}: {sub.Statistics.GetStats().ProcessRate:F2} msg/s");
}
```

### 6. Handle Slow Consumers Appropriately

```csharp
// For real-time data where latest is most important
using var realtimeSubscription = xBar.Subscribe<MarketData>(
    "market.live",
    handler: msg => UpdateDashboard(msg),
    slowConsumerStrategy: SlowConsumerStrategy.SkipUpdates
);

// For critical transactions where every message matters
using var criticalSubscription = xBar.Subscribe<Trade>(
    "trades.execution",
    handler: msg => RecordTrade(msg),
    slowConsumerStrategy: SlowConsumerStrategy.FailSubscription
);
```

### 7. Use Record/Replay for Testing and Debugging

```csharp
// Capture production traffic
using var recorder = xBar.Record("production.>", productionStream, codec);

// Replay against test system
var player = Player<Message>.Create(recordedStream, codec);
await foreach (var msg in player.MessagesAsync())
{
    await testCrossBar.Publish($"test.{msg.Key}", msg.Body);
}
```

### 8. Monitor Your Metrics

```csharp
// Regular health checks
public class HealthMonitor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var channel in _xBar.GetChannels())
            {
                foreach (var sub in _xBar.GetChannelSubscriptions(channel.Name))
                {
                    var stats = sub.Statistics.GetStats(false);

                    if (stats.QueueLength > 10000)
                        _logger.LogWarning($"{sub.Name} queue depth: {stats.QueueLength}");

                    if (stats.AvgLatencyTimeMs > 100)
                        _logger.LogWarning($"{sub.Name} high latency: {stats.AvgLatencyTimeMs}ms");

                    if (sub.GetTimeoutCount() > 0)
                        _logger.LogError($"{sub.Name} handler timeouts: {sub.GetTimeoutCount()}");
                }
            }

            await Task.Delay(5000, ct);
        }
    }
}
```

## API Reference

### ICrossBar Methods

```csharp
// Publishing
ValueTask Publish<TBody>(string channel, TBody? body, ...);

// Subscribing
ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, ...);

// State Management
IEnumerable<Message<TBody>> GetChannelState<TBody>(string channelName);
bool TryGetMessage<TBody>(string channelName, string key, out Message<TBody> message);
bool TryDeleteMessage<TBody>(string channelName, string key, out Message<TBody> message);
void ResetChannel<TBody>(string channelName);
bool TryDeleteChannel(string channelName);

// Recording
IRecording Record<TBody>(string channelOrPattern, Stream stream, IBinaryCodec<TBody> codec, ...);

// Observability
IEnumerable<IChannel> GetChannels();
IEnumerable<ISubscription> GetChannelSubscriptions(string channelName);
void MetricsToJson(Utf8JsonWriter writer, bool useMnemonics = false, bool resetStats = false);
bool MessageTracingEnabled { get; set; }
bool PublishLoggingEnabled { get; set; }

// Utilities
long GetNextCorrelationId();
```

### Message<TBody> Properties

```csharp
public struct Message<TBody>
{
    public long Id { get; }              // Unique message ID
    public long Timestamp { get; }       // UTC timestamp (binary format)
    public MessageType MessageType { get; } // Update, Delete, Reset, Trace
    public long CorrelationId { get; }   // Link related messages
    public string? Key { get; }          // State key
    public string? From { get; }         // Source identifier
    public TBody? Body { get; }          // Payload
    public long TagA { get; }            // Custom metadata
}
```

## Integration with ASP.NET Core

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register CrossBar as singleton
builder.Services.AddSingleton<ICrossBar>(new CrossBar());

// Register your services
builder.Services.AddHostedService<StockPriceProducer>();
builder.Services.AddHostedService<StockPriceConsumer>();

var app = builder.Build();
app.Run();
```

For more examples, check out the [Sample Application](./Berberis.SampleApp/) in this repository.

Contributing
We appreciate any contributions to improve Berberis CrossBar. Please read our Contributing Guide for guidelines about how to proceed.

License
Berberis CrossBar is licensed under the GPL-3 license.

Contact
If you have any questions or suggestions, feel free to open an issue on GitHub.

Thank you for considering Berberis CrossBar for your messaging needs!