using System.Collections.Concurrent;
using SseStocksApi.Models;

namespace SseStocksApi.Services;

public class StockPriceService
{
    private readonly ConcurrentDictionary<string, Stock> _stocks = new();

    private readonly Random _random = new();

    public StockPriceService()
    {
        // Seed with a few fake stocks
        var now = DateTime.UtcNow;

        AddOrUpdate(new Stock
        {
            Symbol = "INFY",
            Name = "Infosys Ltd",
            Price = 1550m,
            PreviousPrice = 1550m,
            LastUpdatedUtc = now
        });

        AddOrUpdate(new Stock
        {
            Symbol = "TCS",
            Name = "Tata Consultancy Services",
            Price = 3900m,
            PreviousPrice = 3900m,
            LastUpdatedUtc = now
        });

        AddOrUpdate(new Stock
        {
            Symbol = "RELI",
            Name = "Reliance Industries",
            Price = 2800m,
            PreviousPrice = 2800m,
            LastUpdatedUtc = now
        });
    }

    private void AddOrUpdate(Stock stock)
    {
        _stocks[stock.Symbol] = stock;
    }

    /// <summary>
    /// Randomly tweaks each stock price and returns the current list.
    /// This will be called each time we push an SSE update.
    /// </summary>
    public IReadOnlyCollection<Stock> GetUpdatedPrices()
    {
        foreach (var key in _stocks.Keys)
        {
            var s = _stocks[key];

            // Keep previous price for UI comparison
            s.PreviousPrice = s.Price;

            // Random % change between -1% and +1%
            var percentChange = (decimal)(_random.NextDouble() * 2 - 1) / 100m;
            var delta = s.Price * percentChange;

            s.Price = Math.Round(s.Price + delta, 2);
            if (s.Price <= 0)
            {
                s.Price = 1; // avoid negative or zero prices
            }

            s.LastUpdatedUtc = DateTime.UtcNow;
            _stocks[key] = s;
        }

        return _stocks.Values
            .OrderBy(s => s.Symbol)
            .ToArray();
    }
}
