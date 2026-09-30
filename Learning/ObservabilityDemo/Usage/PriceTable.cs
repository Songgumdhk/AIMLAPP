using AIMLAPP.Configuration;

namespace AIMLAPP.Learning.ObservabilityDemo.Usage;

// Rates per 1M tokens come from the Pricing section of appsettings.json.
// Cached input is billed cheaper in production. This table uses the full
// input price so the estimate is a ceiling, not a surprise discount.
public sealed record ModelPrice(string Model, decimal InputPerMillion, decimal OutputPerMillion)
{
    // Cost = tokens / 1M × rate, priced separately for input and output.
    // Output is the pricier side: 4× input on gpt-4o and gpt-4o-mini (§5).
    // Decimal, not double, so 0.0075 stays exact in the worked example.
    public decimal Cost(int inputTokens, int outputTokens) =>
        (inputTokens / 1_000_000m) * InputPerMillion
        + (outputTokens / 1_000_000m) * OutputPerMillion;
}

// The rate card, keyed by model name. Kept separate from the trace on purpose:
// the trace stores tokens, this table turns them into dollars. When prices change,
// edit appsettings.json → Pricing and old traces reprice. See 06-Observability.md §5.
public static class PriceTable
{
    public static ModelPrice Chat => For(AppSettings.Current.OpenAI.ChatModel);
    public static ModelPrice SmallChat => For(AppSettings.Current.OpenAI.SmallChatModel);
    public static ModelPrice Embedding => For(AppSettings.Current.OpenAI.EmbeddingModel);

    // WHY throw: a missing price must stop the cost view, not silently print $0.
    public static ModelPrice For(string model) =>
        AppSettings.Current.Pricing.TryGetValue(model, out var price)
            ? new ModelPrice(model, price.InputPerMillion, price.OutputPerMillion)
            : throw new InvalidOperationException(
                $"No price for model '{model}'. Add it under Pricing in appsettings.json.");
}
