using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StockDemo.Models;
using StockDemo.Services;

namespace StockDemo.Pages;

// The page's "controller": gets the data ready before the HTML is drawn.
public class IndexModel : PageModel
{
    private readonly IPriceService _prices;

    public IndexModel(IPriceService prices)
    {
        _prices = prices;
    }

    // Filled in from the address, for example /?Symbol=IBM
    [BindProperty(SupportsGet = true)]
    public string? Symbol { get; set; }

    public PriceResult Bitcoin { get; set; } = new();
    public PriceResult? Stock { get; set; }

    public async Task OnGetAsync()
    {
        Bitcoin = await _prices.GetBitcoinPriceAsync();

        if (!string.IsNullOrWhiteSpace(Symbol))
        {
            Stock = await _prices.GetStockPriceAsync(Symbol);
        }
    }
}
