using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

public interface IRecording : IDisposable
{
    Task MessageLoop { get; }

    ISubscription UnderlyingSubscription { get; }

    RecordingStats RecordingStats { get; }
}