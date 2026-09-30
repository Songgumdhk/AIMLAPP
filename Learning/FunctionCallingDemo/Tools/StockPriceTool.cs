using System.Text.Json;

namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// EXERCISE #1 — Ask: "What are the prices of MSFT and GOOG?"
// You should see two tool calls in a SINGLE assistant response (parallel).
public static class StockPriceTool
{
    public const string Name = "get_stock_price";

    public static OpenAI.Chat.ChatTool Schema => OpenAI.Chat.ChatTool.CreateFunctionTool(
        functionName: Name,
        // WHY "single" + "once per symbol in parallel": a one-item-per-call tool
        // plus an explicit hint is what makes the model batch calls in one turn.
        functionDescription:
            "Get the current stock price for a single ticker symbol " +
            "(e.g. MSFT, GOOG, AAPL). Use this whenever the user asks " +
            "about stock prices or share values. If the user asks about " +
            "multiple symbols, call this tool once per symbol in parallel.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "symbol": {
                    "type": "string",
                    "description": "Ticker symbol, e.g. 'MSFT'."
                }
            },
            "required": ["symbol"]
        }
        """));

    public static Task<string> ExecuteAsync(JsonElement args)
    {
        // Normalize model input: it may send "msft" or "MSFT" (§5).
        var symbol = (args.GetProperty("symbol").GetString() ?? "UNKN").ToUpperInvariant();

        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["MSFT"] = 432.15m,
            ["GOOG"] = 178.42m,
            ["AAPL"] = 226.78m,
            ["NVDA"] = 128.09m,
            ["TSLA"] = 251.44m,
        };

        var price = prices.TryGetValue(symbol, out var p)
            ? p
            : 50m + (symbol.Sum(c => (int)c) % 300);

        // Tool results are just strings to the model. Returning labelled JSON makes
        // it easy for the model to quote the right number and currency.
        var json = JsonSerializer.Serialize(new { symbol, price = $"${price:F2}", currency = "USD" });
        return Task.FromResult(json);
    }
}
