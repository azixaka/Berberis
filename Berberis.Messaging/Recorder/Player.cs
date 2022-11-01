using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;

namespace Berberis.Recorder;

public sealed partial class Player<TBody> : IPlayer
{
    private ICrossBar _crossBar;
    private string _channel;
    private Stream _stream;
    private IMessageBodySerializer<TBody> _serialiser;
    private PlayMode _playMode;

    private int _isPaused = 0;
    private SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

    private Player() { }

    private void Start(ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser,
                        PlayMode playMode, CancellationToken token)
    {
        _crossBar = crossBar;
        _channel = channel;
        _stream = stream;
        _serialiser = serialiser;
        _playMode = playMode;

        MessageLoop = Start(token);
    }

    internal static IPlayer CreatePlayer(ICrossBar crossBar, string channel, Stream stream,
        IMessageBodySerializer<TBody> serialiser, PlayMode playMode, CancellationToken token)
    {
        var player = new Player<TBody>();
        player.Start(crossBar, channel, stream, serialiser, playMode, token);
        return player;
    }

    private async Task Start(CancellationToken token)
    {
        await Task.Yield();

        while (!token.IsCancellationRequested)
        {
            if (!_gate.Wait(0))
                await _gate.WaitAsync(token);

            try
            {
                var chunk = await GetNextChunk();

                if (!chunk.HasValue)
                    break;

                var obj = _serialiser.Derialise(chunk.Value.Body);

                await _crossBar.Publish($"{_channel}{chunk.Value.Key}", obj, chunk.Value.Key, false);
            }
            finally
            { 
                _gate.Release();
            }
        }
    }

    private async ValueTask<MessageChunk?> GetNextChunk()
    {
        var headerBuffer = new byte[MessageCodec.HeaderSize];
        var rcvdCnt = await _stream.ReadAsync(headerBuffer);

        if (rcvdCnt == 0)
            return null;

        while (rcvdCnt < MessageCodec.HeaderSize)
        {
            var rcvdBytes = await _stream.ReadAsync(headerBuffer, rcvdCnt, MessageCodec.HeaderSize - rcvdCnt);

            if (rcvdBytes == 0)
                return null;

            rcvdCnt += rcvdBytes;
        }

        var totalMsgLen = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);

        var buffer = ArrayPool<byte>.Shared.Rent(totalMsgLen);

        try
        {
            var bufferMemory = buffer.AsMemory();

            headerBuffer.AsSpan().CopyTo(bufferMemory.Span);

            var rcvdBytes = await _stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt);

            if (rcvdBytes == 0)
                return null;

            rcvdCnt += rcvdBytes;

            while (rcvdCnt < totalMsgLen)
            {
                rcvdBytes = await _stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt);

                if (rcvdBytes == 0)
                    return null;

                rcvdCnt += rcvdBytes;
            }

            var msgChunk = new MessageChunk(buffer, totalMsgLen);

            return msgChunk;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return null;
    }

    public async ValueTask<bool> Pause(CancellationToken token)
    {
        if (Interlocked.Exchange(ref _isPaused, 1) == 0)
        {
            if (!_gate.Wait(0))
                await _gate.WaitAsync(token);

            return true;
        }

        return false;
    }

    public bool Resume()
    {
        if (Interlocked.Exchange(ref _isPaused, 0) == 1)
        {
            _gate.Release();
            return true;
        }

        return false;
    }

    public Task MessageLoop { get; private set; }

    public void Dispose()
    {
    }
}
