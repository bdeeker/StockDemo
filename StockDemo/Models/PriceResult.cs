namespace StockDemo.Models;

// One result card: either a price, or a reason there isn't one.
public class PriceResult
{
    public bool Success { get; set; }
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
    public decimal? Change { get; set; }
    public string? ChangePercent { get; set; }
    public string? ErrorMessage { get; set; }
}
