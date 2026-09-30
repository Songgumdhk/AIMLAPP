using AIMLAPP.Configuration;

namespace AIMLAPP.Learning.ObservabilityDemo.Pipeline;

// Model names for this chapter, read from appsettings.json → OpenAI.
// Spans record these names, and PriceTable looks up rates by the same key.
public static class ModelNames
{
    // The small model keeps the lesson cheap to repeat. The cost view also prices the
    // same token counts as the full chat model so you can see the bill you would have paid.
    public static string Chat => AppSettings.Current.OpenAI.SmallChatModel;
    public static string Embed => AppSettings.Current.OpenAI.EmbeddingModel;
}
