# Berberis Recorder

High-performance recording and playback of CrossBar messages to binary streams.

## Overview

**Recording**: Capture messages from CrossBar → binary stream
**Playback**: Read messages from binary stream → `IAsyncEnumerable<Message<T>>`
**Performance**: ~10M msg/s recording (zero-alloc), ~5-10M msg/s playback (minimal-alloc)

```csharp
// Recording
using var stream = File.Create("recording.bin");
using var recording = crossBar.Record<MyData>("channel", stream, serializer);
await recording.MessageLoop;

// Playback
using var stream = File.OpenRead("recording.bin");
var player = Player<MyData>.Create(stream, serializer, PlayMode.AsFastAsPossible);
await foreach (var msg in player.MessagesAsync())
    ProcessMessage(msg);
```

## Binary Format

Wire format (all little-endian):

```
Offset  Size    Field
------  ------  --------------------------------------------------
0       4       Length Prefix (total message size, int32)
4       2       Body Offset (uint16)
6       1       Message Type (0=Update, 1=Snapshot, 2=Disconnected)
7       1       Message Version (always 1)
8       2       Reserved (always 0)
10      2       Serializer Version (major, minor)
12      8       Message ID (int64)
20      8       Timestamp (.NET ticks, int64)
28      4       Key Length (int32, -1 for null)
32      N       Key Data (UTF-8)
32+N    4       From Length (int32, -1 for null)
36+N    M       From Data (UTF-8)
36+N+M  X       Message Body (IMessageBodySerializer format)
---     4       Length Suffix (same as prefix, for validation)

Fixed overhead: 32 bytes + Key/From lengths
Max message: 2GB (int32.MaxValue)
```

**Corruption Detection**: Length prefix must equal suffix, else `InvalidDataException`

## Recording Metadata

Recordings can include optional metadata in a separate `.meta.json` file for self-documenting recordings.

**Metadata File**: `{recording}.meta.json` (e.g., `recording.rec` → `recording.rec.meta.json`)

**Format** (JSON):
```json
{
  "created": "2025-10-25T12:30:00Z",
  "channel": "trade.updates",
  "serializerType": "ProtobufSerializer",
  "serializerVersion": 1,
  "messageType": "TradeData",
  "messageCount": 1000000,
  "firstMessageTicks": 638655564000000000,
  "lastMessageTicks": 638655568000000000,
  "durationMs": 40000,
  "indexFile": "recording.rec.idx",
  "custom": {
    "application": "TradingEngine",
    "version": "1.2.3",
    "datacenter": "us-east-1"
  }
}
```

**Usage**:
```csharp
// Create metadata
var metadata = new RecordingMetadata
{
    CreatedUtc = DateTime.UtcNow,
    Channel = "trade.updates",
    SerializerType = "ProtobufSerializer",
    SerializerVersion = 1,
    MessageType = "TradeData",
    Custom = new Dictionary<string, string>
    {
        ["application"] = "TradingEngine",
        ["version"] = "1.2.3"
    }
};

// Record with metadata (FileStream only)
using var stream = File.Create("recording.rec");
using var recording = crossBar.Record("trade.updates", stream, serializer,
    saveInitialState: false, conflationInterval: TimeSpan.Zero, metadata: metadata);

// Read metadata later
var meta = await RecordingMetadata.ReadAsync("recording.rec.meta.json");
Console.WriteLine($"Channel: {meta.Channel}, Messages: {meta.MessageCount}");
```

**Benefits**:
- Inspect recording properties without deserializing
- Version validation before playback
- Better error messages (serializer mismatches)
- Foundation for tooling (merge, split, filter)

**Note**: Metadata is optional. Recordings without metadata work normally (backwards compatible).

## Recording Index & Seeking

Index files enable fast seeking to arbitrary positions in large recordings (e.g., "jump to message 1,000,000" or "start from 10 minutes ago").

**Index File**: `{recording}.idx` (e.g., `recording.rec` → `recording.rec.idx`)

**Format** (binary):
```
Offset  Size    Field
------  ------  --------------------------------------------------
0       4       Magic ("RIDX" = 0x58444952)
4       2       Version (uint16 = 1)
6       2       Reserved
8       4       Index Interval (messages between index entries)
12      8       Total Messages (int64)
20      8       Entry Count (int64)
28      N*24    Index Entries (MessageNumber:int64, FileOffset:int64, Timestamp:int64)
```

**Usage**:
```csharp
// Build index from existing recording
await RecordingIndex.BuildAsync(
    recordingPath: "recording.rec",
    indexPath: "recording.rec.idx",
    serializer: new MySerializer(),
    interval: 1000  // Index every 1000th message
);

// Create indexed player
await using var stream = File.OpenRead("recording.rec");
var player = await IndexedPlayer<MyData>.CreateAsync(
    stream,
    indexPath: "recording.rec.idx",
    serializer: new MySerializer()
);

Console.WriteLine($"Total messages: {player.TotalMessages}");

// Seek to message 500,000
await player.SeekToMessageAsync(500_000);

// Or seek to timestamp (10 minutes ago)
var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10).Ticks;
await player.SeekToTimestampAsync(tenMinutesAgo);

// Play from that point
await foreach (var msg in player.MessagesAsync(cancellationToken))
{
    ProcessMessage(msg);
}
```

**Index Size**:
- Interval 1000: ~240 bytes per 1M messages (~240KB for 1B messages)
- Interval 100: ~2.4KB per 1M messages (~2.4MB for 1B messages)
- Tradeoff: Smaller interval = more precise seeking but larger index file

**Seek Accuracy**:
- Seeks to the nearest indexed message at or before the target
- If interval = 1000, seeking to message 12,345 lands at message 12,000
- Sequential playback from there to reach exact message

**Benefits**:
- Fast navigation: O(log N) seek time via binary search
- Memory efficient: Index loaded entirely in memory (~240KB for 1B messages @ interval 1000)
- Backwards compatible: Recordings without indexes still work (sequential playback)

## Progress Reporting

Track playback progress for long-running operations (e.g., "52% complete, 5.2M messages processed").

**Usage**:
```csharp
var progress = new Progress<RecordingProgress>(p =>
{
    Console.WriteLine($"Progress: {p.PercentComplete:F1}% " +
                      $"({p.MessagesProcessed:N0} messages, " +
                      $"{p.BytesProcessed:N0} / {p.TotalBytes:N0} bytes)");
});

// Playback with progress
await using var stream = File.OpenRead("large-recording.rec");
var player = Player<MyData>.Create(stream, serializer, PlayMode.AsFastAsPossible, progress);

await foreach (var msg in player.MessagesAsync(cancellationToken))
{
    ProcessMessage(msg);
}

// Index building with progress
await RecordingIndex.BuildAsync(
    recordingPath: "recording.rec",
    indexPath: "recording.rec.idx",
    serializer: serializer,
    progress: progress
);
```

**Reporting Frequency**:
- Player: Every 1000 messages
- IndexBuilder: Every 1000 messages
- Configurable via Progress<T> implementation

**RecordingProgress**:
- `BytesProcessed`: Bytes read so far
- `TotalBytes`: Total bytes in stream (0 if not seekable)
- `MessagesProcessed`: Messages processed count
- `PercentComplete`: 0-100% (0 if stream not seekable)

## Recording Utilities

The `RecordingUtilities` class provides tools for manipulating recording files.

### Merge Recordings

Combine multiple recordings into a single file, ordered by timestamp:

```csharp
var metadata = await RecordingUtilities.MergeAsync(
    inputPaths: new[] { "recording1.rec", "recording2.rec" },
    outputPath: "merged.rec",
    serializer: serializer,
    duplicateStrategy: RecordingUtilities.DuplicateStrategy.KeepFirst,
    progress: new Progress<RecordingProgress>(p =>
        Console.WriteLine($"Merging: {p.PercentComplete}%"))
);
```

**Duplicate Strategies**:
- `KeepFirst`: Keep first occurrence of duplicate message ID
- `KeepLast`: Keep last occurrence of duplicate message ID
- `KeepAll`: Keep all messages including duplicates

### Split Recordings

Split large recordings into smaller chunks:

```csharp
// Split by message count (1000 messages per chunk)
var chunks = await RecordingUtilities.SplitAsync(
    inputPath: "large.rec",
    outputPathPattern: "chunk_{0}.rec",
    serializer: serializer,
    splitBy: RecordingUtilities.SplitBy.MessageCount,
    splitValue: 1000
);

// Split by time duration (1 hour chunks)
var chunks = await RecordingUtilities.SplitAsync(
    inputPath: "large.rec",
    outputPathPattern: "chunk_{0}.rec",
    serializer: serializer,
    splitBy: RecordingUtilities.SplitBy.TimeDuration,
    splitValue: TimeSpan.FromHours(1).Ticks
);

// Split by file size (100MB chunks)
var chunks = await RecordingUtilities.SplitAsync(
    inputPath: "large.rec",
    outputPathPattern: "chunk_{0}.rec",
    serializer: serializer,
    splitBy: RecordingUtilities.SplitBy.FileSize,
    splitValue: 100 * 1024 * 1024
);
```

### Filter Recordings

Extract messages matching a predicate:

```csharp
// Filter by key
var metadata = await RecordingUtilities.FilterAsync(
    inputPath: "recording.rec",
    outputPath: "filtered.rec",
    serializer: serializer,
    predicate: msg => msg.Key == "important"
);

// Filter by time range
var cutoff = DateTime.UtcNow.AddHours(-1).Ticks;
var metadata = await RecordingUtilities.FilterAsync(
    inputPath: "recording.rec",
    outputPath: "recent.rec",
    serializer: serializer,
    predicate: msg => msg.Timestamp >= cutoff
);

// Filter by content
var metadata = await RecordingUtilities.FilterAsync(
    inputPath: "recording.rec",
    outputPath: "errors.rec",
    serializer: serializer,
    predicate: msg => msg.Body!.Contains("ERROR")
);
```

### Convert Between Serializer Versions

Migrate recordings to a new serializer version:

```csharp
var metadata = await RecordingUtilities.ConvertAsync(
    inputPath: "old_format.rec",
    outputPath: "new_format.rec",
    oldSerializer: oldSerializerV1,
    newSerializer: newSerializerV2
);
```

**Use Cases**:
- **Merge**: Combine recordings from multiple nodes/sources
- **Split**: Archive management (daily/hourly chunks), size limits
- **Filter**: Extract specific keys, time ranges, error messages
- **Convert**: Migrate to new serializer versions, format upgrades

## Serializer Versioning

```csharp
public record struct SerializerVersion(byte Major, byte Minor);
```

**Version checking**:
- Major mismatch → Exception (incompatible)
- Minor mismatch → Warning (backward compatible)

**Rules**:
- Increment Major for breaking changes (removed fields, type changes)
- Increment Minor for additions (optional fields, non-breaking changes)

## Performance

**Recording**: ~10M msg/s, 0 allocations/msg
- Uses System.IO.Pipelines for zero-copy buffering
- Latency: <100ns (sync path), backpressure if stream slow

**Playback**: ~5-10M msg/s, minimal allocations
- Header buffer: ArrayPool (28 bytes)
- Message buffer: ArrayPool (full message)
- Key/From: Cached after first access

**Tuning**:
- Fast recording: SSD/NVMe, simple serializers, 64KB+ buffer, `useAsync: true`
- Fast playback: BufferedStream, `PlayMode.AsFastAsPossible`, efficient deserializer

## Usage

### Recording

```csharp
using var stream = new FileStream("rec.bin", FileMode.Create, FileAccess.Write,
                                  FileShare.None, 64*1024, useAsync: true);
using var recording = crossBar.Record<MyData>(
    channel: "trade.updates",
    stream: stream,
    serialiser: new MyDataSerializer());

await recording.MessageLoop;  // CRITICAL: Wait for completion

// Monitor stats
var stats = recording.RecordingStats;
Console.WriteLine($"{stats.Count} msgs, {stats.TotalBytes} bytes, " +
                  $"{stats.AverageTimePerMessageMs:F3}ms/msg");
```

**Critical**:
- Always await `MessageLoop` before disposing
- Recording does NOT own the stream (caller disposes it)
- Flush stream periodically for long recordings

### Playback

```csharp
using var fileStream = File.OpenRead("rec.bin");
using var bufferedStream = new BufferedStream(fileStream, 64*1024);
var player = Player<MyData>.Create(bufferedStream, serializer);

await foreach (var msg in player.MessagesAsync(cancellationToken))
{
    ProcessMessage(msg);
}
// Loop exits naturally at end of recording
```

**PlayMode options**:
- `AsFastAsPossible` (default): No delays, maximum throughput
- `RespectOriginalMessageIntervals`: Preserve original timing (uses `Task.Delay`)

**Critical**:
- Player does NOT own the stream (caller disposes it)
- Use `BufferedStream` for better performance (64KB+ buffer)

## Supported Streams

**Works with any Stream**:
- `FileStream` ✅ (recommended for persistence)
- `MemoryStream` ✅ (in-memory recordings)
- `NetworkStream` ✅ (TCP only, ensure reliable connection)
- `GZipStream` ✅ (compression, 50-90% size reduction)
- `CryptoStream` ✅ (encryption)
- `PipeStream` ✅ (inter-process communication)

**Requirements**:
- Recording: Write/WriteAsync support, sequential writes
- Playback: Read/ReadAsync support, sequential reads (seeking not required)

## Common Issues

**Recording slow** (high `AverageTimePerMessageMs`):
- Slow disk → Use SSD/NVMe
- Small buffer → Increase to 64KB+
- Wrong FileStream flags → Use `useAsync: true`

**Playback slow**:
- Using `RespectOriginalMessageIntervals` → Switch to `AsFastAsPossible`
- Unbuffered stream → Wrap in `BufferedStream`
- Heavy processing in loop → Offload to separate task/Channel

**Messages missing from recording**:
- Didn't await `MessageLoop` → Always wait before disposing
- Stream not flushed → Always dispose stream after Recording

**InvalidDataException "Corrupted message"**:
- File truncated → Recording not closed properly
- Disk corruption → Check disk health
- Wrong file → Verify it's actually a recording

**InvalidOperationException "version mismatch"**:
- Recording created with different serializer version
- Use matching version or implement version migration

**OutOfMemoryException during playback**:
- Corrupted length prefix → File corrupted, reading billions of bytes
- Extremely large message → Expected if message >available memory

## Advanced

**Multiple channels** (separate recordings):
```csharp
var rec1 = crossBar.Record<DataA>("channel1", stream1, serializer1);
var rec2 = crossBar.Record<DataB>("channel2", stream2, serializer2);
```

**Compression** (wrap stream):
```csharp
var fileStream = File.Create("rec.bin.gz");
var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
var recording = crossBar.Record<T>(channel, gzipStream, serializer);
```

**Encryption** (wrap stream):
```csharp
var cryptoStream = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write);
var recording = crossBar.Record<T>(channel, cryptoStream, serializer);
```

**Heavy processing** (decouple from playback):
```csharp
var channel = Channel.CreateBounded<Message<MyData>>(1000);

var producer = Task.Run(async () => {
    await foreach (var msg in player.MessagesAsync(ct))
        await channel.Writer.WriteAsync(msg, ct);
    channel.Writer.Complete();
});

var consumer = Task.Run(async () => {
    await foreach (var msg in channel.Reader.ReadAllAsync(ct))
        await HeavyProcessing(msg);
});

await Task.WhenAll(producer, consumer);
```

**Real-time network streaming**:
```csharp
// Server: Record to TCP stream
var server = new TcpListener(IPAddress.Any, 5000);
server.Start();
var client = await server.AcceptTcpClientAsync();
var recording = crossBar.Record<T>(channel, client.GetStream(), serializer);

// Client: Playback from TCP stream
var client = new TcpClient("server", 5000);
var player = Player<T>.Create(client.GetStream(), serializer);
```

## Limitations

- No appending to existing recordings
- No multiplexing (one channel per stream)
- Max message size: 2GB
- Index files must be built post-recording (not during recording)
