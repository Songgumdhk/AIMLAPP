using AIMLAPP.Configuration;

namespace AIMLAPP.Learning.ProductionAiDemo.Deployment;

// The values a deployed process should read from configuration (appsettings.json -> ProductionAi).
// The API key is not a field here. Callers only report whether a key is configured.
// The value is never stored on this object.
public sealed record AppConfig(
    string Model,
    int MaxOutputTokens,
    bool GuardrailsEnabled,
    int RequestsPerMinute,
    int CacheMaxEntries,
    bool LogPrompts)
{
    // Settings are layered: appsettings.json, then appsettings.Local.json, then
    // environment variables (e.g. ProductionAi__RequestsPerMinute). See 07-ProductionAI.md §6.
    public static AppConfig Demo { get; } = FromSettings(AppSettings.Current);

    // Reports presence only. The key's value is never printed, logged, or copied here.
    public static bool ApiKeyIsConfigured =>
        !string.IsNullOrWhiteSpace(AppSettings.Current.OpenAI.ApiKey);

    private static AppConfig FromSettings(AppSettings settings) => new(
        Model: settings.OpenAI.SmallChatModel,
        MaxOutputTokens: settings.ProductionAi.MaxOutputTokens,
        GuardrailsEnabled: settings.ProductionAi.GuardrailsEnabled,
        RequestsPerMinute: settings.ProductionAi.RequestsPerMinute,
        CacheMaxEntries: settings.ProductionAi.CacheMaxEntries,
        LogPrompts: settings.ProductionAi.LogPrompts);
}
