using StockDemo.Models;

namespace StockDemo.Services;

// The job description: anything that can look up prices.
public interface IPriceService
{
    Task<PriceResult> GetBitcoinPriceAsync();
    Task<PriceResult> GetStockPriceAsync(string symbol);
}
