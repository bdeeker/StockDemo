using StockDemo.Services;

// ============================================================
// SETUP: start the app and hire the helpers
// ============================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddScoped<IPriceService, PriceService>();
builder.Services.AddRazorPages();

var app = builder.Build();


// ============================================================
// THE PIPELINE: what every request walks through
// ============================================================

app.UseStaticFiles();
app.MapRazorPages();


// ============================================================
// API ENDPOINTS: the same prices, as JSON, for other programs
// ============================================================

app.MapGet("/api/bitcoin", async (IPriceService prices) => await prices.GetBitcoinPriceAsync());

app.MapGet("/api/stock/{symbol}", async (string symbol, IPriceService prices) => await prices.GetStockPriceAsync(symbol));


// ============================================================
// OPEN THE DOORS
// ============================================================

app.Run();
