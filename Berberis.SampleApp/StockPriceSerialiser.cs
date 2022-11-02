using Berberis.Recorder;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Berberis.SampleApp;

public sealed class StockPriceSerialiser : IMessageBodySerializer<StockPrice>
{
    public SerializerVersion Version { get; } = new SerializerVersion(1, 0);

    public StockPrice Deserialize(ReadOnlySpan<byte> data)
    {
        var len = BinaryPrimitives.ReadInt32LittleEndian(data);

        string symbol = null;

        if (data.Length >= len + 4)
        {
            symbol = Encoding.UTF8.GetString(data.Slice(4, len));
        }

        var price = BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(len + 4));

        return new StockPrice(symbol, price);
    }

    public void Serialize(StockPrice value, IBufferWriter<byte> writer)
    {
        var lenLocation = writer.GetSpan(4);
        writer.Advance(4);
        var length = (int) Encoding.UTF8.GetBytes(value.Symbol, writer);
        BinaryPrimitives.WriteInt32LittleEndian(lenLocation, length);

        BinaryPrimitives.WriteDoubleLittleEndian(writer.GetSpan(8), value.Price);
        writer.Advance(8);
    }
}
