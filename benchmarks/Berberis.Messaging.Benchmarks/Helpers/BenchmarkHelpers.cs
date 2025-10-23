using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Berberis.Messaging;

namespace Berberis.Messaging.Benchmarks.Helpers;

public static class BenchmarkHelpers
{
    /// <summary>
    /// Creates a CrossBar instance for benchmarking with null logger
    /// </summary>
    public static CrossBar CreateBenchmarkCrossBar()
    {
        return new CrossBar(NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Creates a simple test message
    /// </summary>
    public static Message<T> CreateMessage<T>(T body, string? key = null)
    {
        return new Message<T>(
            id: -1,
            timestamp: DateTime.UtcNow.ToBinary(),
            messageType: MessageType.ChannelUpdate,
            correlationId: 0,
            key: key,
            inceptionTicks: Stopwatch.GetTimestamp(),
            from: null,
            body: body,
            tagA: 0
        );
    }

    /// <summary>
    /// Simple data class for benchmarks
    /// </summary>
    public class BenchmarkData
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// Small message payload (typical use case)
    /// </summary>
    public static BenchmarkData CreateSmallPayload(int id) => new()
    {
        Id = id,
        Value = "test",
        Timestamp = DateTime.UtcNow.ToBinary()
    };

    /// <summary>
    /// Medium message payload
    /// </summary>
    public static BenchmarkData CreateMediumPayload(int id) => new()
    {
        Id = id,
        Value = new string('x', 1024), // 1KB string
        Timestamp = DateTime.UtcNow.ToBinary()
    };

    /// <summary>
    /// Large message payload
    /// </summary>
    public static BenchmarkData CreateLargePayload(int id) => new()
    {
        Id = id,
        Value = new string('x', 10240), // 10KB string
        Timestamp = DateTime.UtcNow.ToBinary()
    };
}
