namespace Berberis.SampleApp;

public struct StockPrice
{
    public StockPrice(string symbol, double price)
    {
        Symbol = symbol;
        Price = price;
    }

    public string Symbol;
    public double Price;

    public override string ToString() => $"{Symbol.PadLeft(4)}={Price:N4}";
}
