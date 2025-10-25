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

- No seeking (sequential playback only)
- No appending to existing recordings
- No multiplexing (one channel per stream)
- No built-in merge/split utilities
- Max message size: 2GB
