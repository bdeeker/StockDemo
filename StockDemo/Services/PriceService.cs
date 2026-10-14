using System.Globalization;
using System.Text.Json;
using StockDemo.Models;

namespace StockDemo.Services;

// The worker: actually calls Coinbase and Alpha Vantage.
public class PriceService : IPriceService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PriceService> _logger;

    public PriceService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<PriceService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    // ------------------------------------------------------------
    // BITCOIN (Coinbase, no key needed)
    // ------------------------------------------------------------
    public async Task<PriceResult> GetBitcoinPriceAsync()
    {
        var result = new PriceResult { Name = "Bitcoin" };

        try
        {
            var http = _httpFactory.CreateClient();
            var json = await http.GetStringAsync("https://api.coinbase.com/v2/prices/BTC-USD/spot");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data)
                && data.TryGetProperty("amount", out var amount)
                && decimal.TryParse(amount.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                result.Success = true;
                result.Price = price;
                return result;
            }

            _logger.LogWarning("Coinbase replied without a usable price: {Reply}", json);
            result.ErrorMessage = "Coinbase answered, but not with a price we could read.";
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not get the Bitcoin price from Coinbase");
            result.ErrorMessage = "The Bitcoin price isn't available right now. Please try again later.";
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Coinbase sent back something that wasn't JSON");
            result.ErrorMessage = "Coinbase sent back something we couldn't read.";
            return result;
        }
    }

    // ------------------------------------------------------------
    // STOCKS (Alpha Vantage, free key needed)
    // ------------------------------------------------------------
    public async Task<PriceResult> GetStockPriceAsync(string symbol)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var result = new PriceResult { Name = symbol };

        // Check the key before calling
        var apiKey = _config["AlphaVantage:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "PASTE_YOUR_KEY_HERE")
        {
            result.ErrorMessage = "The stock API key is missing. See README.md, Part 1, Step 2.";
            return result;
        }

        // Build the address. EscapeDataString makes what the visitor typed safe to put in a URL.
        var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(symbol)}&apikey={apiKey}";

        try
        {
            var http = _httpFactory.CreateClient();
            var json = await http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Global Quote", out var quote)
                && quote.TryGetProperty("05. price", out var priceElement)
                && decimal.TryParse(priceElement.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                result.Success = true;
                result.Price = price;

                // Extras: today's change. Nice to have, so we only add them if they're there.
                if (quote.TryGetProperty("09. change", out var changeElement)
                    && decimal.TryParse(changeElement.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var change))
                {
                    result.Change = change;
                }

                if (quote.TryGetProperty("10. change percent", out var percentElement))
                {
                    result.ChangePercent = percentElement.GetString();
                }

                return result;
            }

            // It answered, but not with a price. Log the reply (never the url: it contains the key).
            _logger.LogWarning("No price for {Symbol}. Alpha Vantage replied: {Reply}", symbol, json);
            result.ErrorMessage = $"No price found for {symbol}. Check the symbol, or the daily limit may be used up.";
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach Alpha Vantage for {Symbol}", symbol);
            result.ErrorMessage = "The stock price service isn't available right now. Please try again later.";
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Alpha Vantage sent back something that wasn't JSON for {Symbol}", symbol);
            result.ErrorMessage = "The stock service sent back something we couldn't read.";
            return result;
        }
    }
}
