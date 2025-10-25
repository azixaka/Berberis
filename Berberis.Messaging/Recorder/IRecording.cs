using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

/// <summary>
/// Message recording interface.
/// </summary>
/// <remarks>
/// <para><strong>Allocation Guarantee:</strong></para>
/// The recording implementation provides zero allocations per message on the hot path.
/// All message data is buffered through System.IO.Pipelines which reuses internal buffers.
/// This makes recording suitable for high-throughput, low-latency scenarios.
/// </remarks>
public interface IRecording : IDisposable
{
    /// <summary>Gets the message processing loop task.</summary>
    Task MessageLoop { get; }

    /// <summary>Gets the underlying subscription that receives messages.</summary>
    ISubscription UnderlyingSubscription { get; }

    /// <summary>Gets recording statistics.</summary>
    RecorderStats RecordingStats { get; }
}