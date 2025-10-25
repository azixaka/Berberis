# Berberis CrossBar Messaging

[![CI](https://github.com/azixaka/Berberis/actions/workflows/ci.yml/badge.svg)](https://github.com/azixaka/Berberis/actions/workflows/ci.yml)
[![Nuget](https://img.shields.io/nuget/v/Berberis.Messaging)](https://www.nuget.org/packages/Berberis.Messaging/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Berberis.Messaging)](https://www.nuget.org/packages/Berberis.Messaging/)

Berberis CrossBar is a high-performance, allocation-free in-process message broker designed for creating complex, high-speed pipelines within a single process. Built on the concept of typed channels, Berberis CrossBar serves as the bridge connecting publishers and subscribers within your application.

## Features

- **Typed Channels**: Each channel in Berberis CrossBar is a typed destination, creating a clear and efficient path for message passing between publishers and subscribers.

- **Message Conflation**: Berberis CrossBar supports message conflation, enhancing the efficiency of your messaging system by preventing the overloading of channels with redundant or unnecessary data.

- **Record/Replay**: Capture message streams to binary files with zero-allocation recording and efficient playback. Features include metadata files for self-describing recordings, fast indexing for seeking in large files, progress reporting, and utilities for merging, splitting, and filtering recordings. Streaming index creation enables instant seeking without post-processing.

- **Comprehensive Observability**: With Berberis CrossBar, you can trace not only messages but also a wide array of statistics, including service time, latencies, rates, sources, percentiles, and more. Lifecycle tracking enables real-time topology visualization by publishing events when channels and subscriptions are created or destroyed. This empowers you to gain deeper insights into the performance of your messaging system and make data-driven optimizations.

- **Stateful Channels**: Berberis CrossBar offers stateful channels, which store the latest published messages by key. This allows new subscribers to fetch the most recent state of the channel upon subscription, keeping everyone up-to-date and in sync.

- **Channel Reset and Message Deletions**: Berberis CrossBar also supports channel resets, allowing you to clear a channel and start fresh when necessary. Individual message deletions are also supported, ensuring that you have full control over the data in your channels.

- **Wildcard Subscriptions**: The system supports wildcard subscription patterns like '*' and '>', offering you more flexibility and control over the messages you subscribe to.

- **Metrics Export**: With the MetricsToJson extension method, you can easily generate a comprehensive JSON report of metrics from all CrossBar channels and each subscription. This feature provides an efficient way to monitor and optimize the performance of your messaging system.

## Performance

Berberis CrossBar delivers exceptional throughput and ultra-low latency with a completely allocation-free hot path. Benchmarks run on AMD Ryzen 9 5950X (16 cores, 32 logical processors), Windows 11, .NET 8.0.21:

### Key Metrics

| Metric | Value |
|--------|-------|
| **Pure Publish Throughput** | **~4.57M messages/sec** |
| **Sustained Throughput** | **~3.17M messages/sec** |
| **Single Message Publish** | **287 ns** |
| **End-to-End Latency** | **873 ns** |
| **Hot Path Allocations** | **0 bytes** |

### Core Performance

**Allocation-Free Hot Path** (proves zero-allocation operation):
```
Method                    | Mean        | Allocated
------------------------- | ----------- | ---------
Single Publish            | 251.4 ns    | 0 B
100 Publishes             | 20,749 ns   | 0 B  (~4.82M msg/s)
```

**Publish/Subscribe Operations**:
```
Operation                 | Time        | Throughput
------------------------- | ----------- | ----------
Single Publish            | 286.9 ns    | ~3.49M msg/s
Publish + Receive         | 1,722.8 ns  | ~580K msg/s
100 Messages              | 21.9 μs     | ~4.57M msg/s
```

**Sustained Throughput** (with subscriber processing):
```
Messages  | Time       | Throughput    | Allocations
--------- | ---------- | ------------- | -----------
1,000     | 274.1 μs   | ~3.65M msg/s  | 0 B
10,000    | 2,852 μs   | ~3.51M msg/s  | 3 B
100,000   | 31,575 μs  | ~3.17M msg/s  | 23 B
```

**Concurrent Publishing** (8 concurrent publishers):
```
Publishers | Messages Each | Total Time  | Throughput
---------- | ------------- | ----------- | ----------
2          | 1,000         | 416.8 μs    | ~4.79M msg/s
4          | 1,000         | 791.7 μs    | ~5.05M msg/s
8          | 1,000         | 2,391.3 μs  | ~3.35M msg/s
```

**Multiple Subscribers** (fan-out performance):
```
Subscribers | Time per Publish
----------- | ----------------
1           | 274.4 ns
3           | 1,496.3 ns  (~499 ns/subscriber)
10          | 5,800.5 ns  (~580 ns/subscriber)
```

**Latency Distribution**:
```
Operation                 | Mean      | P50       | P90       | P95
------------------------- | --------- | --------- | --------- | ---------
Publish → Receive         | 872.9 ns  | 876.9 ns  | 878.0 ns  | 878.1 ns
Synchronous Handler       | 253.9 ns  | -         | -         | -
Async Handler (no await)  | 158.2 ns  | -         | -         | -
```

**Message Size Independence** (payload size has negligible impact):
```
Payload Size | Time
------------ | ---------
Small        | 270.5 ns
1 KB         | 279.3 ns
10 KB        | 278.1 ns
```

> **Platform**: AMD Ryzen 9 5950X, Windows 11 (10.0.26200.6901), .NET 8.0.21 X64 RyuJIT AVX2

For complete benchmark results with all scenarios (stateful channels, wildcards, conflation, etc.), see the [benchmarks](./benchmarks) directory.

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

### Configuration Options

CrossBar can be configured with centralized defaults and system-wide settings using `CrossBarOptions`:

```csharp
var options = new CrossBarOptions
{
    DefaultBufferCapacity = 5000,                           // Null = unbounded (default), or set value for bounded
    DefaultSlowConsumerStrategy = SlowConsumerStrategy.DropOldest,
    DefaultConflationInterval = TimeSpan.FromMilliseconds(100),
    MaxChannels = 1000,                                     // Limit total channels
    MaxChannelNameLength = 512,                             // Max channel name length
    EnableMessageTracing = false,                           // System-wide tracing
    EnablePublishLogging = false,                           // Verbose publish logging
    SystemChannelPrefix = "$",                              // Prefix for system channels
    SystemChannelBufferCapacity = 1000                      // Buffer for system channels
};

ICrossBar xBar = new CrossBar(loggerFactory, options);

// Subscriptions automatically use configured defaults
xBar.Subscribe<StockPrice>("stock.prices", async msg =>
{
    // Uses configured defaults (or original Berberis defaults if options = null)
    Console.WriteLine($"{msg.Body.Symbol}: ${msg.Body.Price}");
});
```

### ASP.NET Core Integration

CrossBar integrates seamlessly with ASP.NET Core's configuration and dependency injection:

**appsettings.json:**
```json
{
  "CrossBar": {
    "DefaultBufferCapacity": 5000,
    "MaxChannels": 1000,
    "EnableMessageTracing": false
  }
}
```

**Program.cs:**
```csharp
// Configure options from appsettings.json
builder.Services.Configure<CrossBarOptions>(
    builder.Configuration.GetSection("CrossBar"));

// Register CrossBar as singleton
builder.Services.AddSingleton<ICrossBar>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var options = sp.GetRequiredService<IOptions<CrossBarOptions>>().Value;
    return new CrossBar(loggerFactory, options);
});
```

**Usage in Services:**
```csharp
public class OrderService
{
    private readonly ICrossBar _xBar;

    public OrderService(ICrossBar xBar)
    {
        _xBar = xBar;
        // All subscriptions use config from appsettings.json
        _xBar.Subscribe<Order>("orders.new", ProcessOrder);
    }
}
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

Capture message streams to binary files for debugging, testing, or auditing. See [docs/Recorder.md](docs/Recorder.md) for detailed documentation.

```csharp
using Berberis.Recorder;
using System.Buffers;
using System.Buffers.Binary;

// Define a serializer for your message type
public class StockPriceSerializer : IMessageBodySerializer<StockPrice>
{
    public SerializerVersion Version => new(1, 0);

    public void Serialize(StockPrice value, IBufferWriter<byte> writer)
    {
        var span = writer.GetSpan(value.Symbol.Length * 3 + 12);
        var bytesWritten = Encoding.UTF8.GetBytes(value.Symbol, span.Slice(4));
        BinaryPrimitives.WriteInt32LittleEndian(span, bytesWritten);
        BinaryPrimitives.WriteDoubleLittleEndian(span.Slice(4 + bytesWritten), value.Price);
        writer.Advance(4 + bytesWritten + 8);
    }

    public StockPrice Deserialize(ReadOnlySpan<byte> data)
    {
        var symbolLength = BinaryPrimitives.ReadInt32LittleEndian(data);
        var symbol = Encoding.UTF8.GetString(data.Slice(4, symbolLength));
        var price = BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(4 + symbolLength));
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
            serialiser: new StockPriceSerializer(),
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
        // PlayMode.RespectOriginalMessageIntervals preserves original timing
        var player = Player<StockPrice>.Create(
            fileStream,
            new StockPriceSerializer(),
            PlayMode.AsFastAsPossible);

        await foreach (var message in player.MessagesAsync(stoppingToken))
        {
            // Republish to the same or different channel
            await _xBar.Publish("replay.stock.prices", message.Body, key: message.Key);
        }
    }
}

// Recording with Metadata: Add self-describing metadata to recordings
public class RecorderWithMetadataService : BackgroundService
{
    private readonly ICrossBar _xBar;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var metadata = new RecordingMetadata
        {
            CreatedUtc = DateTime.UtcNow,
            Channel = "stock.prices.>",
            SerializerType = "StockPriceSerializer",
            SerializerVersion = 1,
            MessageType = "StockPrice",
            Custom = new Dictionary<string, string>
            {
                ["application"] = "StockRecorder",
                ["version"] = "1.0.0"
            }
        };

        using var fileStream = File.OpenWrite("stock-prices.bin");
        using var recording = _xBar.Record(
            channelOrPattern: "stock.prices.>",
            stream: fileStream,
            serialiser: new StockPriceSerializer(),
            saveInitialState: false,
            conflationInterval: TimeSpan.Zero,
            metadata: metadata,
            cancellationToken: stoppingToken
        );

        await Task.Delay(10_000, stoppingToken);
        recording.Dispose();
        await recording.MessageLoop;

        // Metadata is automatically written to "stock-prices.bin.meta.json"
    }
}

// Reading Metadata: Inspect recording properties without deserializing
var meta = await RecordingMetadata.ReadAsync("stock-prices.bin.meta.json");
if (meta != null)
{
    Console.WriteLine($"Channel: {meta.Channel}");
    Console.WriteLine($"Serializer: {meta.SerializerType} v{meta.SerializerVersion}");
    Console.WriteLine($"Message Count: {meta.MessageCount}");
}

// Index & Seek: Fast navigation in large recordings
await RecordingIndex.BuildAsync(
    recordingPath: "stock-prices.bin",
    indexPath: "stock-prices.bin.idx",
    serializer: new StockPriceSerializer(),
    interval: 1000  // Index every 1000 messages
);

await using var stream = File.OpenRead("stock-prices.bin");
var indexedPlayer = await IndexedPlayer<StockPrice>.CreateAsync(
    stream,
    indexPath: "stock-prices.bin.idx",
    serializer: new StockPriceSerializer()
);

Console.WriteLine($"Total messages: {indexedPlayer.TotalMessages}");

// Seek to message 500,000
await indexedPlayer.SeekToMessageAsync(500_000);

// Or seek to 10 minutes ago
var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10).Ticks;
await indexedPlayer.SeekToTimestampAsync(tenMinutesAgo);

// Progress Reporting: Track playback progress
var progress = new Progress<RecordingProgress>(p =>
{
    Console.WriteLine($"Progress: {p.PercentComplete:F1}% " +
                      $"({p.MessagesProcessed:N0} messages processed)");
});

await using var progressStream = File.OpenRead("stock-prices.bin");
var progressPlayer = Player<StockPrice>.Create(
    progressStream,
    new StockPriceSerializer(),
    PlayMode.AsFastAsPossible,
    progress
);

await foreach (var msg in progressPlayer.MessagesAsync(CancellationToken.None))
{
    ProcessMessage(msg);
}

// Streaming Index: Build index during recording (no post-processing!)
var metadata = new RecordingMetadata
{
    CreatedUtc = DateTime.UtcNow,
    Channel = "stock.prices",
    IndexFile = "stock-prices.bin.idx",  // Index built automatically during recording
    SerializerType = "StockPriceSerializer",
    SerializerVersion = 1,
    MessageType = "StockPrice"
};

using var fileStream = File.OpenWrite("stock-prices.bin");
using var recording = _xBar.Record(
    "stock.prices",
    fileStream,
    new StockPriceSerializer(),
    metadata: metadata  // Index will be built as messages are recorded!
);

// Messages are being recorded AND indexed simultaneously
// No need to call RecordingIndex.BuildAsync() later!

// Recording Utilities: Merge, split, filter, and convert recordings
using Berberis.Recorder;

// Merge multiple recordings into one (by timestamp)
var mergedMeta = await RecordingUtilities.MergeAsync<StockPrice>(
    inputPaths: new[] { "rec1.bin", "rec2.bin", "rec3.bin" },
    outputPath: "merged.bin",
    serializer: new StockPriceSerializer(),
    duplicateHandling: DuplicateHandling.KeepLast  // KeepFirst, KeepLast, or KeepAll
);

// Split large recording into smaller chunks
var splitMetas = await RecordingUtilities.SplitAsync<StockPrice>(
    inputPath: "large-recording.bin",
    outputPathPattern: "chunk-{0:D4}.bin",  // chunk-0001.bin, chunk-0002.bin, etc.
    serializer: new StockPriceSerializer(),
    splitBy: SplitCriteria.MessageCount(1_000_000)  // Or TimeDuration, FileSize
);

// Filter recording by predicate
var filteredMeta = await RecordingUtilities.FilterAsync<StockPrice>(
    inputPath: "all-stocks.bin",
    outputPath: "aapl-only.bin",
    serializer: new StockPriceSerializer(),
    predicate: msg => msg.Body?.Symbol == "AAPL"
);

// Convert between serializer versions
var convertedMeta = await RecordingUtilities.ConvertAsync<StockPrice>(
    inputPath: "old-format.bin",
    outputPath: "new-format.bin",
    oldSerializer: new StockPriceSerializerV1(),
    newSerializer: new StockPriceSerializerV2()
);
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

#### Lifecycle Tracking for Topology Visualization

Berberis CrossBar provides lifecycle event tracking to monitor channel and subscription creation and deletion in real-time. This is ideal for building dynamic topology visualizations, dashboards, and monitoring systems.

**Enable lifecycle tracking:**

```csharp
// Option 1: Enable at initialization
var options = new CrossBarOptions { EnableLifecycleTracking = true };
var xBar = new CrossBar(loggerFactory, options);

// Option 2: Enable at runtime
xBar.LifecycleTrackingEnabled = true;

// Subscribe to lifecycle events
xBar.Subscribe<LifecycleEvent>(
    "$lifecycle",  // System channel for lifecycle events
    msg =>
    {
        var evt = msg.Body;

        switch (evt.EventType)
        {
            case LifecycleEventType.ChannelCreated:
                Console.WriteLine($"Channel created: {evt.ChannelName} ({evt.MessageBodyType})");
                // Update your topology graph: add channel node
                break;

            case LifecycleEventType.SubscriptionCreated:
                Console.WriteLine($"Subscription created: {evt.SubscriptionName} → {evt.ChannelName}");
                // Update your topology graph: add subscription node and edge
                break;

            case LifecycleEventType.SubscriptionDisposed:
                Console.WriteLine($"Subscription disposed: {evt.SubscriptionName}");
                // Update your topology graph: remove subscription node
                break;

            case LifecycleEventType.ChannelDeleted:
                Console.WriteLine($"Channel deleted: {evt.ChannelName}");
                // Update your topology graph: remove channel node
                break;
        }

        return ValueTask.CompletedTask;
    }
);
```

**LifecycleEvent structure:**

```csharp
public readonly struct LifecycleEvent
{
    public LifecycleEventType EventType { get; init; }  // ChannelCreated, ChannelDeleted, SubscriptionCreated, SubscriptionDisposed
    public string ChannelName { get; init; }            // Channel name or wildcard pattern
    public string? SubscriptionName { get; init; }      // Subscription name (null for channel events)
    public string MessageBodyType { get; init; }        // Message type (e.g., "MyApp.Order")
    public DateTime Timestamp { get; init; }            // Event timestamp (UTC)
}
```

**Use cases:**
- **Real-time topology visualization**: Automatically update graphs showing publishers, channels, and subscribers
- **Monitoring dashboards**: Track subscription churn, channel usage patterns
- **Audit trails**: Record when components connect/disconnect from the messaging system
- **Debugging**: Understand the dynamic behavior of your messaging topology

**Performance characteristics:**
- Zero hot-path allocations (events only published on channel/subscription create/destroy)
- Minimal overhead: ~100-200ns per lifecycle event
- No impact on message processing throughput

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
IRecording Record<TBody>(string channelOrPattern, Stream stream, IMessageBodySerializer<TBody> serialiser, ...);

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