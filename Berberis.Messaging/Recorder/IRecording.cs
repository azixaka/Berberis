using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

/// <summary>Message recording interface.</summary>
public interface IRecording : IDisposable
{
    /// <summary>Gets the message processing loop task.</summary>
    Task MessageLoop { get; }

    /// <summary>Gets the underlying subscription that receives messages.</summary>
    ISubscription UnderlyingSubscription { get; }

    /// <summary>Gets recording statistics.</summary>
    RecorderStats RecordingStats { get; }
}