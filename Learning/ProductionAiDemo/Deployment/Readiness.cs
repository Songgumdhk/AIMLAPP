namespace AIMLAPP.Learning.ProductionAiDemo.Deployment;

public sealed record ReadinessCheck(string Name, bool Passed, string Detail);

// Fail closed: a shipped process with guardrails off or an empty model name
// is not ready. These checks do not call the network.
// A bad value in config or an environment variable is caught before the first user
// request, not after it. See 07-ProductionAI.md §6 for the table.
public static class Readiness
{
    public static IReadOnlyList<ReadinessCheck> Run(AppConfig config)
    {
        return
        [
            Check("model", !string.IsNullOrWhiteSpace(config.Model), "Model name is set."),
            // No cap, or a huge one, lets each answer's output cost run away.
            Check("max output tokens", config.MaxOutputTokens is > 0 and <= 500,
                "MaxOutputTokens is between 1 and 500."),
            Check("guardrails", config.GuardrailsEnabled, "Guardrails are on."),
            Check("rate limit", config.RequestsPerMinute > 0, "RequestsPerMinute is above 0."),
            Check("cache bound", config.CacheMaxEntries is > 0 and <= 10_000,
                "The cache has a maximum size."),
            // Full prompts can hold user PII and retrieved private text. Logs keep them.
            Check("prompt logging", !config.LogPrompts, "Full prompts are not written to logs."),
        ];
    }

    private static ReadinessCheck Check(string name, bool passed, string detail) =>
        new(name, passed, passed ? detail : "Not ready: " + detail);
}
