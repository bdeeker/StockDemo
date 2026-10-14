# StockDemo: a Razor web page that calls two real APIs

A small ASP.NET Core app that shows live prices on a styled web page.

| Address | What you see |
| --- | --- |
| `/` | The web page: a Bitcoin card, and a stock lookup box |
| `/?Symbol=IBM` | The same page with IBM's price filled in |
| `/api/bitcoin` | The Bitcoin price as JSON, for other programs |
| `/api/stock/IBM` | IBM's price as JSON |

---

## Part 1: Run it

### Step 1: Open it

Double click `StockDemo.sln`.

### Step 2: Add your stock API key (the safe way)

Get a free key from **alphavantage.co/support/#api-key** (25 calls a day).

**Don't type the real key into appsettings.json**, because that file goes to GitHub. Use **User Secrets**, which live on your own computer and are never uploaded:

1. In **Solution Explorer**, right click the **StockDemo** project (not the solution)
2. Click **Manage User Secrets**
3. Replace everything in `secrets.json` with:

```json
{
  "AlphaVantage": {
    "ApiKey": "YOUR_REAL_KEY"
  }
}
```

4. Save it

On a new computer, repeat this step. Secrets don't travel with the code.

### Step 3: Run it

Press **F5**. The page opens with the Bitcoin price. Type **IBM** in the box and click **Get price**.

---

## Part 2: How the pieces fit together

```
Browser
   |
   v
Pages/Index.cshtml  ......  the HTML you see (the "view")
   ^
   |  gets its data from
   |
Pages/Index.cshtml.cs  ...  gets the data ready (like a controller)
   |
   |  asks for prices from
   v
Services/IPriceService  ..  the job description
Services/PriceService  ...  the worker that calls the APIs
   |
   v
Coinbase and Alpha Vantage
```

**The big idea:** each file has one job.
- The **service** only knows how to get prices. It knows nothing about HTML.
- The **page** only knows how to show prices. It knows nothing about Coinbase.

This is called **separation of concerns**. It's why the `/api/...` endpoints can reuse the same service with one line each. Vowlio is built the same way: controllers ask services for data, and Razor views show it.

---

## Part 3: The files, line by line

### File 1: Program.cs (the setup)

```csharp
using StockDemo.Services;
```
"I'm going to use things from the Services folder." Needed so this file knows what `IPriceService` and `PriceService` are.

```csharp
var builder = WebApplication.CreateBuilder(args);
```
Start setting up the app. The manager picks up the clipboard.

```csharp
builder.Services.AddHttpClient();
```
Hire the helper that can call other websites.

```csharp
builder.Services.AddScoped<IPriceService, PriceService>();
```
**The same line pattern as Vowlio's Program.cs.** "When anyone asks for an `IPriceService` (the job description), send them a `PriceService` (the worker)." **Scoped** means a new one for each page load.

```csharp
builder.Services.AddRazorPages();
```
Turn on Razor Pages, the feature that turns `.cshtml` files into web pages.

```csharp
var app = builder.Build();
```
Finish the setup.

```csharp
app.UseStaticFiles();
```
Let the browser download files from the `wwwroot` folder, like our CSS file. Without this line, the page would load with no styling.

```csharp
app.MapRazorPages();
```
Connect each page file to an address. `Pages/Index.cshtml` becomes the home page, `/`.

```csharp
app.MapGet("/api/bitcoin", async (IPriceService prices) => await prices.GetBitcoinPriceAsync());
```
A second front door for the same data. Visit `/api/bitcoin` and you get JSON instead of a web page, like this:
```json
{"success":true,"name":"Bitcoin","price":83891.58,"change":null,"changePercent":null,"errorMessage":null}
```
This is **your own API**. Another program could call it, just like your code calls Coinbase.

```csharp
app.MapGet("/api/stock/{symbol}", async (string symbol, IPriceService prices) => await prices.GetStockPriceAsync(symbol));
```
The same thing for stocks. `{symbol}` is whatever is typed in the address.

```csharp
app.Run();
```
Open the doors.

---

### File 2: Models/PriceResult.cs (the result card)

```csharp
public class PriceResult
{
    public bool Success { get; set; }
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
    public decimal? Change { get; set; }
    public string? ChangePercent { get; set; }
    public string? ErrorMessage { get; set; }
}
```
This is a **blank form** that holds one answer. It's the same idea as `VendorMatchResult` in Vowlio.

| Field | Type | Holds |
| --- | --- | --- |
| `Success` | true or false | Did we get a price? |
| `Name` | text | "Bitcoin" or "IBM" |
| `Price` | number | The price, like 233.10 |
| `Change` | number | How much it moved today (stocks only) |
| `ChangePercent` | text | Like "0.69%" (stocks only) |
| `ErrorMessage` | text | If it failed, a friendly reason why |

- `decimal` is the number type for money. It doesn't have the tiny rounding errors other number types have.
- The `?` means "this can be empty." A failed lookup has no price.
- `= ""` gives Name a starting value, so it's never empty.

**Why return a result card instead of just text?** Last night the code returned sentences like `"IBM price: $233.10"`. A web page can't style a sentence. It needs the **pieces**, the name, the number and whether it worked, so it can put each piece in the right place with the right color.

---

### File 3: Services/IPriceService.cs (the job description)

```csharp
public interface IPriceService
{
    Task<PriceResult> GetBitcoinPriceAsync();
    Task<PriceResult> GetStockPriceAsync(string symbol);
}
```
Two duties:
- Get the Bitcoin price, and hand back a result card
- Get a stock price for a symbol, and hand back a result card

`Task<...>` means "this takes a moment, I'll hand it back when it's ready."

---

### File 4: Services/PriceService.cs (the worker)

This file holds the same API code from last night, moved out of Program.cs into its own class.

#### The constructor

```csharp
public class PriceService : IPriceService
```
"This worker does the IPriceService job." The colon means "fills this job description."

```csharp
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PriceService> _logger;
```
Three empty shelves to keep the helpers on.

```csharp
    public PriceService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<PriceService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }
```
The "who I need" list. .NET hands over the internet helper, the settings reader and the logger, and they go on the shelves. **This is exactly the Vowlio constructor pattern.**

#### The Bitcoin method

```csharp
    public async Task<PriceResult> GetBitcoinPriceAsync()
    {
        var result = new PriceResult { Name = "Bitcoin" };
```
Start with a blank result card and write "Bitcoin" on it. `Success` starts as false, so if anything goes wrong, the card already says "didn't work."

```csharp
        try
        {
            var http = _httpFactory.CreateClient();
            var json = await http.GetStringAsync("https://api.coinbase.com/v2/prices/BTC-USD/spot");
```
Same as last night: get a connection, call Coinbase, wait for the answer.

```csharp
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
```
Turn the text into boxes, and start at the outside.

```csharp
            if (root.TryGetProperty("data", out var data)
                && data.TryGetProperty("amount", out var amount)
                && decimal.TryParse(amount.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
```
Three checks, joined by `&&` (**and**). All three must pass:
1. Is there a box called `data`? If yes, call it `data`.
2. Inside it, is there `amount`? If yes, call it `amount`.
3. Can that text be turned into a real number? If yes, call it `price`.

New piece: **`decimal.TryParse`**. The price arrives as **text**, like `"83891.58"`. To format it as money (with commas and a dollar sign), we need a **number**. `TryParse` tries to convert it. If the text isn't a number, it says no instead of crashing.
- `NumberStyles.Number` allows a minus sign and a decimal point
- `CultureInfo.InvariantCulture` means "read the dot as a decimal point." Without it, a computer set to a European language might read `83891.58` wrong.

```csharp
            {
                result.Success = true;
                result.Price = price;
                return result;
            }
```
All checks passed. Fill in the card and hand it back.

```csharp
            _logger.LogWarning("Coinbase replied without a usable price: {Reply}", json);
            result.ErrorMessage = "Coinbase answered, but not with a price we could read.";
            return result;
        }
```
**Kind 2 failure:** Coinbase answered, but not with what we expected. Log a warning with what it said, write a friendly reason on the card, and hand it back.

```csharp
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not get the Bitcoin price from Coinbase");
            result.ErrorMessage = "The Bitcoin price isn't available right now. Please try again later.";
            return result;
        }
```
**Kind 1 failure:** couldn't reach Coinbase at all. Log the error, write a friendly reason, hand the card back.

```csharp
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Coinbase sent back something that wasn't JSON");
            result.ErrorMessage = "Coinbase sent back something we couldn't read.";
            return result;
        }
```
**New:** a second catch. If the reply isn't JSON at all (for example an HTML error page), `JsonDocument.Parse` throws a `JsonException`. A `try` can have several `catch` blocks, one for each kind of problem.

#### The stock method

```csharp
    public async Task<PriceResult> GetStockPriceAsync(string symbol)
    {
        symbol = symbol.Trim().ToUpperInvariant();
```
**New:** clean up what the visitor typed.
- `.Trim()` removes spaces at the start and end, so `" ibm "` becomes `"ibm"`
- `.ToUpperInvariant()` makes it capitals, so `"ibm"` becomes `"IBM"`

```csharp
        var result = new PriceResult { Name = symbol };
```
A blank card with the symbol written on it.

```csharp
        var apiKey = _config["AlphaVantage:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "PASTE_YOUR_KEY_HERE")
        {
            result.ErrorMessage = "The stock API key is missing. See README.md, Part 1, Step 2.";
            return result;
        }
```
Read the key from settings. If it's missing, stop before calling. This is a **guard clause**.

```csharp
        var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(symbol)}&apikey={apiKey}";
```
Build the address. **New: `Uri.EscapeDataString(symbol)`.** Now that the symbol comes from a **text box**, a visitor could type anything, including characters like `&` or `?` that have special meaning in an address. `EscapeDataString` makes whatever they typed safe to put in a URL. **Never trust what a user types.** That's a good security point for the interview.

```csharp
        try
        {
            var http = _httpFactory.CreateClient();
            var json = await http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
```
Same pattern: call, wait, turn into boxes.

```csharp
            if (root.TryGetProperty("Global Quote", out var quote)
                && quote.TryGetProperty("05. price", out var priceElement)
                && decimal.TryParse(priceElement.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                result.Success = true;
                result.Price = price;
```
Same three checks as Bitcoin, with Alpha Vantage's box names. If all pass, fill in the price.

```csharp
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
```
**New: optional extras.** Alpha Vantage also sends how much the stock moved today. That's nice to have, not essential. So:
- If the change is there, add it to the card
- If it's not, **skip it quietly**. The price still shows.

Don't let a nice to have piece break the important piece.

```csharp
            _logger.LogWarning("No price for {Symbol}. Alpha Vantage replied: {Reply}", symbol, json);
            result.ErrorMessage = $"No price found for {symbol}. Check the symbol, or the daily limit may be used up.";
            return result;
        }
```
Kind 2 failure: answered, but no price. We log the **reply**, never the **url**, because the url contains the API key.

The two `catch` blocks after this work exactly like the Bitcoin ones.

---

### File 5: Pages/_ViewImports.cshtml (shared settings for all pages)

```
@using StockDemo
@using StockDemo.Models
@namespace StockDemo.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```
- `@using` lines: let every page use things from these folders
- `@namespace`: the address for the page classes
- `@addTagHelper`: turns on **tag helpers**, special attributes like `asp-for` that write HTML for you. It also makes `~/css/site.css` work.

Vowlio has a `_ViewImports.cshtml` too, doing the same job.

---

### File 6: Pages/Index.cshtml.cs (gets the data ready)

This is the **page model**. It plays the same role as a controller in Vowlio.

```csharp
public class IndexModel : PageModel
```
"This is a page model." `PageModel` is .NET's built in base for Razor page code.

```csharp
    private readonly IPriceService _prices;

    public IndexModel(IPriceService prices)
    {
        _prices = prices;
    }
```
The page asks for the **job description**, `IPriceService`, in its constructor. It has no idea Coinbase or Alpha Vantage exist. Same as `AccountController` asking for `IStripeService` in Vowlio.

```csharp
    [BindProperty(SupportsGet = true)]
    public string? Symbol { get; set; }
```
Holds what the visitor typed in the stock box.
- `[BindProperty]` means "fill this in automatically from the request"
- `SupportsGet = true` means "including from the address." When the form submits, the address becomes `/?Symbol=IBM`, and .NET puts `IBM` into this property for you.

```csharp
    public PriceResult Bitcoin { get; set; } = new();
    public PriceResult? Stock { get; set; }
```
Two result cards for the HTML to show.
- `Bitcoin` always gets filled, so it starts as a blank card (`new()`)
- `Stock` has a `?` because it stays empty until someone searches

```csharp
    public async Task OnGetAsync()
    {
        Bitcoin = await _prices.GetBitcoinPriceAsync();

        if (!string.IsNullOrWhiteSpace(Symbol))
        {
            Stock = await _prices.GetStockPriceAsync(Symbol);
        }
    }
```
**`OnGetAsync` runs every time someone loads the page.** The name matters: `OnGet` means "when the page is visited." .NET finds it by name.
1. Always get the Bitcoin price
2. If the visitor typed a symbol, get that stock price too

Then .NET draws the HTML.

---

### File 7: Pages/Index.cshtml (the HTML)

A `.cshtml` file is **HTML with C# mixed in**. Anything starting with `@` is C#.

```
@page
@model IndexModel
```
- `@page` means "this file is a page with its own address." It must be the first line.
- `@model IndexModel` means "my data comes from IndexModel." Now `Model.Bitcoin` and `Model.Stock` are available.

```html
<link rel="stylesheet" href="~/css/site.css" />
```
Loads the stylesheet. `~` means "the wwwroot folder," so this points to `wwwroot/css/site.css`.

#### The Bitcoin card

```html
<section class="card">
    <p class="label">Bitcoin <span class="source">from Coinbase</span></p>
```
A card with a small heading. `class="card"` and `class="label"` connect to styles in the CSS file.

```
    @if (Model.Bitcoin.Success)
    {
        <p class="price">@(Model.Bitcoin.Price?.ToString("C"))</p>
    }
    else
    {
        <p class="error">@Model.Bitcoin.ErrorMessage</p>
    }
```
**C# deciding which HTML to show.**
- If the lookup worked, show the price in big text
- Otherwise, show the error message in a red box

`ToString("C")` means "format as **currency**." It turns `83891.58` into **$83,891.58**, with the comma and dollar sign added for you.

`@( ... )` with brackets is needed when the C# has extra symbols in it, like the `?.`

#### The stock card: the search form

```html
<form method="get" class="search">
    <input asp-for="Symbol" placeholder="e.g. IBM" maxlength="10" autocomplete="off" />
    <button type="submit">Get price</button>
</form>
```
- `method="get"` means submitting puts the answer in the address: `/?Symbol=IBM`. That's what `SupportsGet = true` reads.
- `asp-for="Symbol"` is a **tag helper**. It connects this box to the `Symbol` property. It sets the box's name, and refills it with what you typed after the page reloads.
- `maxlength="10"` stops anyone typing a very long symbol

#### The stock card: the result

```
@if (Model.Stock != null)
{
```
Only show a result if someone searched. `!= null` means "is not empty."

```
    if (Model.Stock.Success)
    {
        <p class="symbol">@Model.Stock.Name</p>
        <p class="price">@(Model.Stock.Price?.ToString("C"))</p>
```
If it worked, show the symbol and the price.

```
        if (Model.Stock.Change.HasValue)
        {
            var isUp = Model.Stock.Change.Value >= 0;
```
If we have today's change: was it up or down? `isUp` is true when the change is zero or more.

```
            <p class="change @(isUp ? "up" : "down")">
```
Picks the CSS class: `up` (green) or `down` (red). The `? :` is the short if/else you've seen before.

```
                @(isUp ? "▲" : "▼") @(Math.Abs(Model.Stock.Change.Value).ToString("C")) (@Model.Stock.ChangePercent) today
```
Shows an arrow, the amount, and the percent, like **▲ $1.60 (0.69%) today**.
`Math.Abs` removes the minus sign, because the red down arrow already says it went down.

```
    else
    {
        <p class="error">@Model.Stock.ErrorMessage</p>
    }
```
If the lookup failed, show the friendly reason in a red box.

---

### File 8: wwwroot/css/site.css (the look)

The main ideas, top to bottom:

| Section | What it does |
| --- | --- |
| `:root { --bg: ... }` | Sets every color **once**, with a name. The rest of the file uses the names, like `var(--accent)`. To change the blue, change one line. |
| `@media (prefers-color-scheme: dark)` | If the computer is in dark mode, the same color names get darker values. The page switches automatically. |
| `.container` | Keeps the page a comfortable width (560px) and centers it |
| `.card` | White box, rounded corners, thin border, soft shadow |
| `.label` | Small grey capitals for card headings |
| `.price` | Big bold number. `tabular-nums` makes every digit the same width, so prices don't jiggle when they change. |
| `.up` / `.down` | Green and red for the daily change |
| `.error` | Light red box for friendly error messages |
| `.search` | Puts the text box and button side by side (`display: flex`), with the box taking the leftover space (`flex: 1`) |

---

## Part 4: The two kinds of failure

| Kind | Example | How the code handles it |
| --- | --- | --- |
| **1. Can't reach it** | No internet, server down, error code | `catch (HttpRequestException)` |
| **2. It answers, but wrong** | Bad symbol, daily limit used up, missing field | `TryGetProperty` and `TryParse` check first |
| **2b. It answers with garbage** | An HTML error page instead of JSON | `catch (JsonException)` |

In every case, the visitor sees a friendly message and the page keeps working. The log gets the details.

---

## Part 5: Try breaking it

1. **Bad symbol:** search for `NOTREAL`. A red box says no price was found. The Output window shows a yellow **warn:** line with Alpha Vantage's reply.
2. **Unreachable:** in PriceService, change `BTC-USD` to `NOTREAL-USD` and run. The Bitcoin card shows a red message, **but the stock search still works.** One failing service doesn't take down the page. Change it back.
3. **Missing key:** in User Secrets, set the key to `""`. Search for IBM. You get the "key is missing" message, and no call is made.
4. **Your own API:** visit `/api/stock/IBM` and see the JSON.

---

## Part 6: How this maps to Vowlio

| This demo | Vowlio |
| --- | --- |
| `AddScoped<IPriceService, PriceService>()` | `AddScoped<IStripeService, StripeService>()` |
| `IPriceService` | `IVendorMatchService` |
| `PriceService` constructor | `VendorMatchService` constructor |
| `PriceResult` | `VendorMatchResult` |
| `IndexModel` (page model) | Controllers, like `VendorMatchController` |
| `Index.cshtml` | Razor views, like `_MatchResults.cshtml` |
| `TryGetProperty` | `TryGetValue` on the plan phone book |
| Friendly message plus log | DeepL returning English plus log |

---

## Part 7: What to say in the interview

> "I keep API calls in a service behind an interface, so the page never knows which vendor is behind it. The service returns a result object instead of throwing, so the page can show either the data or a friendly message. I handle both kinds of failure: the service being unreachable, and it answering with something unexpected. I clean anything a user types before it goes into a URL, I keep keys in User Secrets locally and Azure settings in production, and I never log a key."
