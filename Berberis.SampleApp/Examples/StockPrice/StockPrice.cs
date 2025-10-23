namespace Berberis.SampleApp.Examples.StockPrice;

public struct StockPrice
{
    public StockPrice(string symbol, double price)
    {
        Symbol = symbol;
        Price = price;
    }

    public string Symbol { get; init; }
    public double Price { get; init; }

    public override string ToString() => $"{Symbol.PadLeft(4)}={Price:N4}";
}
