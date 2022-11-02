using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Berberis.Recorder;

public sealed class Recording<TBody> : IRecording
{
    private ISubscription _subscription;
    private Stream _stream;
    private IMessageBodySerializer<TBody> _serialiser;
    private Pipe _pipe;
    private readonly RecordingStatsReporter _recordingStatsReporter = new();
    private volatile bool _ready;

    private readonly CancellationTokenSource _cts = new();

    private Recording() { }

    private void Start(ISubscription subscription, Stream stream, IMessageBodySerializer<TBody> serialiser, CancellationToken token)
    {
        _subscription = subscription;
        _stream = stream;
        _serialiser = serialiser;
        _pipe = new Pipe();

        _ready = true;

        var cts = token == default ? _cts : CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);
        MessageLoop = Task.WhenAll(_subscription.MessageLoop, PipeReaderLoop(cts.Token));
    }

    //todo: change MessageLoop to do WaitAny and handle cases when externally someone disposes our underlying subscription, we should just cancel the PipeReaderLoop too
    public ISubscription UnderlyingSubscription => _subscription;

    public RecordingStats RecordingStats => _recordingStatsReporter.GetStats();

    internal static IRecording CreateRecording(ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser,
                                               bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        var recording = new Recording<TBody>();
        var subscription = crossBar.Subscribe<TBody>(channel, recording.MessageHandler, "Berberis.Recording", saveInitialState, conflationInterval, token);
        recording.Start(subscription, stream, serialiser, token);
        return recording;
    }

    private ValueTask MessageHandler(Message<TBody> message)
    {
        // maybe return "cold subscription" and then activate it when initialised
        if (!_ready)
            return ValueTask.CompletedTask;

        var pipeWriter = _pipe.Writer;

        var messageLengthSpan = MessageCodec.WriteChannelUpdateMessageHeader(pipeWriter, _serialiser.Version, ref message);

        // Write serialised messasge body
        _serialiser.Serialize(message.Body, pipeWriter);

        MessageCodec.WriteMessageLengthPrefixAndSuffix(pipeWriter, messageLengthSpan);

        var result = pipeWriter.FlushAsync();

        if (!result.IsCompletedSuccessfully)
            return AsyncPath(result);

        if (result.Result.IsCompleted)
        {
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;

        async ValueTask AsyncPath(ValueTask<FlushResult> task)
        {
            var flushResult = await task;

            if (flushResult.IsCompleted)
            {
                return;
            }
        }
    }

    private async Task PipeReaderLoop(CancellationToken token)
    {
        await Task.Yield();

        var pipeReader = _pipe.Reader;

        while (!token.IsCancellationRequested)
        {
            try
            {
                ReadResult result = await pipeReader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (true)
                {
                    var ticks = _recordingStatsReporter.Start();
                    var success = TryReadMessage(ref buffer, out ReadOnlySequence<byte> message);
                    if (success)
                    {
                        await ProcessMessage(message);
                        _recordingStatsReporter.Stop(ticks, message.Length);
                    }
                    else break;
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
            catch (OperationCanceledException) { }
        }

        bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
        {
            if (buffer.Length >= 4
                && BinaryPrimitives.TryReadInt32LittleEndian(buffer.FirstSpan, out var msgLen)
                && buffer.Length >= msgLen)
            {
                message = buffer.Slice(0, msgLen);
                buffer = buffer.Slice(msgLen);
                return true;
            }

            message = default;
            return false;
        }

        async Task ProcessMessage(ReadOnlySequence<byte> message)
        {
            foreach (var memory in message)
            {
                await _stream.WriteAsync(memory);
            }
        }
    }

    public Task MessageLoop { get; private set; }

    public void Dispose()
    {
        _cts.Cancel();
        _subscription?.TryDispose();
        _pipe.Writer.Complete();
        _pipe.Reader.Complete();
    }
}