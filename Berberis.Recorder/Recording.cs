using Berberis.Messaging;
using System.Text.Json;

namespace Berberis.Recorder;

public sealed class Recording<TBody> : IRecording
{
    private ISubscription _subscription;
    private Stream _stream;

    private Recording() { }

    private void SetParams(ISubscription subscription, Stream stream)
    {
        _subscription = subscription;
        _stream = stream;
    }

    internal static IRecording CreateRecording(ICrossBar crossBar, string channel, Stream outputStream, bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        var recording = new Recording<TBody>();
        var subscription = crossBar.Subscribe<TBody>(channel, recording.MessageHandler, "Berberis.Recording", saveInitialState, conflationInterval, token);
        recording.SetParams(subscription, outputStream);
        return recording;
    }

    private async ValueTask MessageHandler(Message<TBody> message)
    {
        //if not paused -> handle
        await JsonSerializer.SerializeAsync(_stream, message, new JsonSerializerOptions { MaxDepth = 16 });
    }

    public bool Record()
    {
        return true;
    }

    public bool Pause()
    {
        return false;
    }

    public void Dispose()
    {
        //stop recording, flush
        _subscription?.Dispose();
    }
}