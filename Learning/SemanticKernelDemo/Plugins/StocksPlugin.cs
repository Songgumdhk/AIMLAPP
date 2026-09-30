using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace AIMLAPP.Learning.SemanticKernelDemo.Plugins;

// SK version of Chapter 1's StockPriceTool. The description still says "once per
// symbol, in parallel", so the model batches multiple symbols the same way.
// Data is static, so the plugin is stateless and safe to register with AddFromType.
public class StocksPlugin
{
    private static readonly Dictionary<string, decimal> Prices =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MSFT"] = 432.15m,
            ["GOOG"] = 178.42m,
            ["AAPL"] = 226.78m,
            ["NVDA"] = 128.09m,
            ["TSLA"] = 251.44m,
        };

    [KernelFunction("get_stock_price")]
    [Description("Get the current stock price for a single ticker symbol " +
                 "(e.g. MSFT, GOOG). Call once per symbol; multiple symbols " +
                 "should be called in parallel.")]
    public string GetPrice(
        [Description("Ticker symbol, e.g. 'MSFT'.")] string symbol)
    {
        symbol = symbol.ToUpperInvariant();
        var price = Prices.TryGetValue(symbol, out var p)
            ? p
            : 50m + (symbol.Sum(c => (int)c) % 300);

        // Returning labelled JSON gives the model a clear, structured tool result.
        return JsonSerializer.Serialize(new { symbol, price = $"${price:F2}", currency = "USD" });
    }
}
