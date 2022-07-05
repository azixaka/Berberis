using Berberis.Messaging;
using System.Text.Json;

namespace Berberis.Recorder;

public sealed class Recording<TBody> : IRecording
{
    private ISubscription _subscription;
    private FileStream _stream;

    private Recording() { }

    private void SetSubscription(ISubscription subscription)
    {
        _subscription = subscription;
        _stream = File.OpenWrite($"{_subscription.Name}.json");
    }

    internal static IRecording CreateRecording(ICrossBar crossBar, string channel, string recordingName, bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        var recording = new Recording<TBody>();
        var subscription = crossBar.Subscribe<TBody>(channel, recording.MessageHandler, $"Recording-[{recordingName}]", saveInitialState, conflationInterval, token);
        recording.SetSubscription(subscription);
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