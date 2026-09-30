using Microsoft.Extensions.Configuration;

namespace AIMLAPP.Configuration;

// Every AI-related value lives in appsettings.json; these classes only give it types.
// Load order (later wins): appsettings.json -> appsettings.Local.json -> environment variables.
// appsettings.Local.json is gitignored; secrets belong there or in the environment.
public sealed class AppSettings
{
    private static readonly Lazy<AppSettings> _current = new(Load);

    public static AppSettings Current => _current.Value;

    public ConnectionStringSettings ConnectionStrings { get; set; } = new();
    public OpenAiSettings OpenAI { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public Dictionary<string, ModelPriceSettings> Pricing { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public FunctionCallingSettings FunctionCalling { get; set; } = new();
    public RagSettings Rag { get; set; } = new();
    public AgenticWorkflowSettings AgenticWorkflows { get; set; } = new();
    public ObservabilitySettings Observability { get; set; } = new();
    public ProductionAiSettings ProductionAi { get; set; } = new();

    public string RequireOpenAiApiKey() =>
        !string.IsNullOrWhiteSpace(OpenAI.ApiKey)
            ? OpenAI.ApiKey
            : throw new InvalidOperationException(
                "OpenAI API key is missing. Set OpenAI:ApiKey in appsettings.Local.json " +
                "or the OPENAI_API_KEY environment variable.");

    public string RequireConnectionString() =>
        !string.IsNullOrWhiteSpace(ConnectionStrings.AimlDatabase)
            ? ConnectionStrings.AimlDatabase
            : throw new InvalidOperationException(
                "Connection string is missing. Set ConnectionStrings:AimlDatabase in appsettings.Local.json " +
                "or the ConnectionStrings__AimlDatabase environment variable.");

    private static AppSettings Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables();

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
            builder.AddInMemoryCollection([new("OpenAI:ApiKey", openAiKey)]);

        return builder.Build().Get<AppSettings>() ?? new AppSettings();
    }
}

public sealed class ConnectionStringSettings
{
    public string AimlDatabase { get; set; } = "";
}

public sealed class OpenAiSettings
{
    public string ApiKey { get; set; } = "";
    public string ChatModel { get; set; } = "";
    public string SmallChatModel { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";

    // Must match the SQL Server column type vector(N) and the embedding model's output size.
    public int EmbeddingDimensions { get; set; }

    public float Temperature { get; set; }
}

public sealed class OllamaSettings
{
    public string BaseUrl { get; set; } = "";
    public string ChatModel { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";
    public int RequestTimeoutSeconds { get; set; }
}

public sealed class ModelPriceSettings
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
}

public sealed class FunctionCallingSettings
{
    public int MaxTurns { get; set; }
}

public sealed class RagSettings
{
    public int TopK { get; set; }
    public int RerankCandidates { get; set; }
    public int RrfK { get; set; }
    public int QueryRewriteCount { get; set; }
    public int FixedChunkSize { get; set; }
    public int FixedChunkOverlap { get; set; }
    public int SentencesPerChunk { get; set; }
}

public sealed class AgenticWorkflowSettings
{
    public int ReviewPassScore { get; set; }
    public int MaxRevisions { get; set; }
    public int MaxNodeVisits { get; set; }
    public int MaxAgentHops { get; set; }
}

public sealed class ObservabilitySettings
{
    public int AnswerMaxOutputTokens { get; set; }
    public int CompareMaxOutputTokens { get; set; }
}

public sealed class ProductionAiSettings
{
    public int MaxOutputTokens { get; set; }
    public bool GuardrailsEnabled { get; set; }
    public int RequestsPerMinute { get; set; }
    public int CacheMaxEntries { get; set; }
    public bool LogPrompts { get; set; }
}
