namespace SseStocksApi.Models;

public class Stock
{
    public string Symbol { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public decimal PreviousPrice { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
