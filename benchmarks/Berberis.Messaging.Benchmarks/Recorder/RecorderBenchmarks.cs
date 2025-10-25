using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;
using Berberis.Recorder;

namespace Berberis.Messaging.Benchmarks.Recorder;

/// <summary>
/// Simple int serializer for benchmarks
/// </summary>
internal class BenchmarkIntSerializer : IMessageBodySerializer<int>
{
    public SerializerVersion Version => new SerializerVersion(1, 0);

    public void Serialize(int value, IBufferWriter<byte> writer)
    {
        var span = writer.GetSpan(4);
        BitConverter.TryWriteBytes(span, value);
        writer.Advance(4);
    }

    public int Deserialize(ReadOnlySpan<byte> data)
    {
        return BitConverter.ToInt32(data);
    }
}

/// <summary>
/// Benchmarks for Recording allocation profile
/// Validates zero-allocation claim for recording path
/// </summary>
[MemoryDiagnoser]
public class RecordingAllocationBenchmarks
{
    private CrossBar _crossBar = null!;
    private IRecording _recording = null!;
    private Stream _recordingStream = null!;
    private Message<int> _message;
    private BenchmarkIntSerializer _serializer = null!;
    private const string TestChannel = "benchmark.channel";

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _message = BenchmarkHelpers.CreateMessage(42, key: "test-key");
        _serializer = new BenchmarkIntSerializer();

        // Use memory stream for benchmarking (avoid disk I/O overhead)
        _recordingStream = new MemoryStream();
        _recording = _crossBar.Record(TestChannel, _recordingStream, _serializer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _recording?.Dispose();
        _recordingStream?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark(Description = "Record single message (should be 0 allocations)")]
    public async Task Recording_SingleMessage_AllocationProfile()
    {
        await _crossBar.Publish(TestChannel, _message, store: false);
        await Task.Delay(1); // Allow recording to process
    }

    [Benchmark(Description = "Record 100 messages (should be 0 allocations per message)")]
    public async Task Recording_100Messages_AllocationProfile()
    {
        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish(TestChannel, _message, store: false);
        }
        await Task.Delay(10); // Allow recording to process all
    }
}

/// <summary>
/// Benchmarks for Playback allocation profile
/// Validates low-allocation claim with ArrayPool and property caching
/// </summary>
[MemoryDiagnoser]
public class PlaybackAllocationBenchmarks
{
    private Stream _playbackStream = null!;
    private IPlayer<int> _player = null!;
    private BenchmarkIntSerializer _serializer = null!;
    private const int MessageCount = 1000;

    [GlobalSetup]
    public async Task Setup()
    {
        _serializer = new BenchmarkIntSerializer();

        // Create a recording with known messages
        var stream = new MemoryStream();
        var crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        var recording = crossBar.Record("test.channel", stream, _serializer);

        // Record messages
        for (int i = 0; i < MessageCount; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: $"key-{i}");
            await crossBar.Publish("test.channel", msg, store: false);
        }

        await Task.Delay(100); // Ensure all messages recorded
        recording.Dispose();
        crossBar.Dispose();

        // Prepare for playback
        stream.Position = 0;
        _playbackStream = stream;
        _player = Player<int>.Create(_playbackStream, _serializer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _player?.Dispose();
        _playbackStream?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Reset stream position for each iteration
        _playbackStream.Position = 0;
    }

    [Benchmark(Description = "Playback 100 messages (should be minimal allocations with ArrayPool)")]
    public async Task<int> Playback_100Messages_AllocationProfile()
    {
        int count = 0;
        await foreach (var msg in _player.MessagesAsync(CancellationToken.None))
        {
            count++;
            if (count >= 100) break;
        }
        return count;
    }

    [Benchmark(Description = "Playback 1000 messages (full allocation profile)")]
    public async Task<int> Playback_1000Messages_AllocationProfile()
    {
        int count = 0;
        await foreach (var msg in _player.MessagesAsync(CancellationToken.None))
        {
            count++;
        }
        return count;
    }
}

/// <summary>
/// Benchmarks for Recording throughput
/// Measures messages per second recording capability
/// </summary>
public class RecordingThroughputBenchmarks
{
    private CrossBar _crossBar = null!;
    private IRecording _recording = null!;
    private Stream _recordingStream = null!;
    private Message<int> _message;
    private BenchmarkIntSerializer _serializer = null!;
    private const string TestChannel = "benchmark.throughput";

    [Params(100, 1000, 10000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _message = BenchmarkHelpers.CreateMessage(42, key: "test-key");
        _serializer = new BenchmarkIntSerializer();
        _recordingStream = new MemoryStream();
        _recording = _crossBar.Record(TestChannel, _recordingStream, _serializer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _recording?.Dispose();
        _recordingStream?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark(Description = "Recording throughput (messages/sec)")]
    public async Task<int> Recording_Throughput()
    {
        for (int i = 0; i < MessageCount; i++)
        {
            await _crossBar.Publish(TestChannel, _message, store: false);
        }
        await Task.Delay(MessageCount / 100); // Allow processing
        return MessageCount;
    }
}

/// <summary>
/// Benchmarks for Playback throughput
/// Measures messages per second playback capability
/// </summary>
public class PlaybackThroughputBenchmarks
{
    private Stream _playbackStream = null!;
    private IPlayer<int> _player = null!;
    private BenchmarkIntSerializer _serializer = null!;

    [Params(100, 1000, 10000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _serializer = new BenchmarkIntSerializer();

        // Create recording
        var stream = new MemoryStream();
        var crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        var recording = crossBar.Record("test.channel", stream, _serializer);

        for (int i = 0; i < MessageCount; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i);
            await crossBar.Publish("test.channel", msg, store: false);
        }

        await Task.Delay(MessageCount / 100);
        recording.Dispose();
        crossBar.Dispose();

        stream.Position = 0;
        _playbackStream = stream;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _player?.Dispose();
        _playbackStream?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _playbackStream.Position = 0;
        _player = Player<int>.Create(_playbackStream, _serializer);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _player?.Dispose();
    }

    [Benchmark(Description = "Playback throughput (messages/sec)")]
    public async Task<int> Playback_Throughput()
    {
        int count = 0;
        await foreach (var msg in _player.MessagesAsync(CancellationToken.None))
        {
            count++;
        }
        return count;
    }
}

/// <summary>
/// Simple data serializer for benchmarks
/// </summary>
internal class BenchmarkDataSerializer : IMessageBodySerializer<BenchmarkHelpers.BenchmarkData>
{
    public SerializerVersion Version => new SerializerVersion(1, 0);

    public void Serialize(BenchmarkHelpers.BenchmarkData value, IBufferWriter<byte> writer)
    {
        // Serialize Id (4 bytes)
        var span = writer.GetSpan(4);
        BitConverter.TryWriteBytes(span, value.Id);
        writer.Advance(4);

        // Serialize Value length (4 bytes) + string data
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value.Value);
        span = writer.GetSpan(4 + valueBytes.Length);
        BitConverter.TryWriteBytes(span, valueBytes.Length);
        valueBytes.CopyTo(span.Slice(4));
        writer.Advance(4 + valueBytes.Length);

        // Serialize Timestamp (8 bytes)
        span = writer.GetSpan(8);
        BitConverter.TryWriteBytes(span, value.Timestamp);
        writer.Advance(8);
    }

    public BenchmarkHelpers.BenchmarkData Deserialize(ReadOnlySpan<byte> data)
    {
        int id = BitConverter.ToInt32(data.Slice(0, 4));
        int valueLength = BitConverter.ToInt32(data.Slice(4, 4));
        string value = System.Text.Encoding.UTF8.GetString(data.Slice(8, valueLength));
        long timestamp = BitConverter.ToInt64(data.Slice(8 + valueLength, 8));

        return new BenchmarkHelpers.BenchmarkData
        {
            Id = id,
            Value = value,
            Timestamp = timestamp
        };
    }
}

/// <summary>
/// Benchmarks for different message sizes
/// Tests performance with varying payload sizes
/// </summary>
[MemoryDiagnoser]
public class MessageSizeBenchmarks
{
    private CrossBar _crossBar = null!;
    private IRecording _recording = null!;
    private Stream _recordingStream = null!;
    private BenchmarkDataSerializer _serializer = null!;
    private const string TestChannel = "benchmark.sizes";

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _serializer = new BenchmarkDataSerializer();
        _recordingStream = new MemoryStream();
        _recording = _crossBar.Record(TestChannel, _recordingStream, _serializer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _recording?.Dispose();
        _recordingStream?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Small message (< 100 bytes)")]
    public async Task Recording_SmallMessage()
    {
        var msg = BenchmarkHelpers.CreateMessage(BenchmarkHelpers.CreateSmallPayload(1));
        await _crossBar.Publish(TestChannel, msg, store: false);
        await Task.Delay(1);
    }

    [Benchmark(Description = "Medium message (~1KB)")]
    public async Task Recording_MediumMessage()
    {
        var msg = BenchmarkHelpers.CreateMessage(BenchmarkHelpers.CreateMediumPayload(1));
        await _crossBar.Publish(TestChannel, msg, store: false);
        await Task.Delay(1);
    }

    [Benchmark(Description = "Large message (~10KB)")]
    public async Task Recording_LargeMessage()
    {
        var msg = BenchmarkHelpers.CreateMessage(BenchmarkHelpers.CreateLargePayload(1));
        await _crossBar.Publish(TestChannel, msg, store: false);
        await Task.Delay(1);
    }
}

/// <summary>
/// Benchmarks for property caching optimization
/// Validates that cached properties reduce allocations
/// </summary>
[MemoryDiagnoser]
public class PropertyCachingBenchmarks
{
    private Stream _playbackStream = null!;
    private IPlayer<int> _player = null!;
    private BenchmarkIntSerializer _serializer = null!;
    private const int MessageCount = 100;

    [GlobalSetup]
    public async Task Setup()
    {
        _serializer = new BenchmarkIntSerializer();

        var stream = new MemoryStream();
        var crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        var recording = crossBar.Record("test.channel", stream, _serializer);

        for (int i = 0; i < MessageCount; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: $"key-{i}");
            await crossBar.Publish("test.channel", msg, store: false);
        }

        await Task.Delay(50);
        recording.Dispose();
        crossBar.Dispose();

        stream.Position = 0;
        _playbackStream = stream;
        _player = Player<int>.Create(_playbackStream, _serializer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _player?.Dispose();
        _playbackStream?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _playbackStream.Position = 0;
    }

    [Benchmark(Baseline = true, Description = "Access Key once per message")]
    public async Task<int> PropertyAccess_Single()
    {
        int keyCount = 0;
        await foreach (var msg in _player.MessagesAsync(CancellationToken.None))
        {
            if (msg.Key != null) keyCount++;
        }
        return keyCount;
    }

    [Benchmark(Description = "Access Key multiple times per message (tests caching)")]
    public async Task<int> PropertyAccess_Multiple()
    {
        int keyCount = 0;
        await foreach (var msg in _player.MessagesAsync(CancellationToken.None))
        {
            // Access Key 3 times - should benefit from caching
            if (msg.Key != null) keyCount++;
            if (msg.Key != null) keyCount++;
            if (msg.Key != null) keyCount++;
        }
        return keyCount;
    }
}
