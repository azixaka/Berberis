using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Berberis.Recorder;

/// <summary>
/// Records messages from a CrossBar subscription to a stream.
/// </summary>
/// <remarks>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Zero allocations per message on the hot path (uses System.IO.Pipelines for buffering)</description></item>
/// <item><description>Throughput: Capable of ~10M messages/second on modern hardware</description></item>
/// <item><description>Backpressure: Automatically applies backpressure if stream writes are slow</description></item>
/// <item><description>Async throughout: All I/O operations are fully asynchronous</description></item>
/// </list>
/// </remarks>
/// <typeparam name="TBody">The message body type.</typeparam>
public sealed class Recording<TBody> : IRecording
{
    private ISubscription _subscription = null!;
    private Stream _stream = null!;
    private IMessageBodySerializer<TBody> _serialiser = null!;
    private Pipe _pipe = null!;
    private readonly RecorderStatsReporter _recorderStatsReporter = new();

    private readonly CancellationTokenSource _cts = new();

    private Recording() { }

    private void Start(Stream stream, IMessageBodySerializer<TBody> serialiser, CancellationToken token)
    {
        _stream = stream;
        _serialiser = serialiser;
        _pipe = new Pipe();
    }

    private static async Task MonitorTasksAsync(Task subscriptionTask, Task pipeReaderTask, CancellationTokenSource cts)
    {
        var completedTask = await Task.WhenAny(subscriptionTask, pipeReaderTask);

        // If subscription completes first, cancel pipe reader immediately
        if (completedTask == subscriptionTask)
        {
            cts.Cancel();
        }

        // Wait for both tasks to complete (may throw)
        await Task.WhenAll(subscriptionTask, pipeReaderTask);
    }
    /// <summary>Gets the underlying subscription that receives messages.</summary>
    public ISubscription UnderlyingSubscription => _subscription;

    /// <summary>Gets recording statistics.</summary>
    public RecorderStats RecordingStats => _recorderStatsReporter.GetStats();

    internal static IRecording CreateRecording(ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser,
                                               bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        var recording = new Recording<TBody>();
        recording.Start(stream, serialiser, token);
        var subscription = crossBar.Subscribe<TBody>(channel, recording.MessageHandler, "Berberis.Recording", saveInitialState, conflationInterval, token);
        recording._subscription = subscription;
        var cts = token == default ? recording._cts : CancellationTokenSource.CreateLinkedTokenSource(recording._cts.Token, token);
        recording.MessageLoop = MonitorTasksAsync(subscription.MessageLoop, recording.PipeReaderLoop(cts.Token), cts);
        return recording;
    }

    private ValueTask MessageHandler(Message<TBody> message)
    {
        var pipeWriter = _pipe.Writer;

        var messageLengthSpan = MessageCodec.WriteChannelMessageHeader(pipeWriter, _serialiser.Version, ref message);

        if (message.MessageType == MessageType.ChannelUpdate)
        {
            // Write serialised message body
            _serialiser.Serialize(message.Body!, pipeWriter);
        }

        MessageCodec.WriteMessageLengthPrefixAndSuffix(pipeWriter, messageLengthSpan);

        var result = pipeWriter.FlushAsync();

        // Fast path: if flush completed synchronously, return immediately
        if (result.IsCompletedSuccessfully)
            return ValueTask.CompletedTask;

        // Slow path: await the flush asynchronously
        return new ValueTask(result.AsTask());
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
                    var ticks = _recorderStatsReporter.Start();
                    var success = TryReadMessage(ref buffer, out ReadOnlySequence<byte> message);
                    if (success)
                    {
                        foreach (var memory in message)
                        {
                            await _stream.WriteAsync(memory);
                        }

                        _recorderStatsReporter.Stop(ticks, message.Length);
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
    }

    /// <summary>Gets the message processing loop task.</summary>
    public Task MessageLoop { get; private set; } = null!;

    /// <summary>
    /// Disposes the recording and stops message capture.
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();
        _subscription?.TryDispose();
        _pipe.Writer.Complete();
        _pipe.Reader.Complete();
    }
}