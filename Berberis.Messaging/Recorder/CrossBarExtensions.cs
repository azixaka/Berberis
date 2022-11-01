using Berberis.Messaging;

namespace Berberis.Recorder;

public static class CrossBarExtensions
{
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, CancellationToken token = default)
        => Record(crossBar, channel, stream, serialiser, false, TimeSpan.Zero, token);

    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
        => Recording<TBody>.CreateRecording(crossBar, channel, stream, serialiser, saveInitialState, conflationInterval, token);
}
