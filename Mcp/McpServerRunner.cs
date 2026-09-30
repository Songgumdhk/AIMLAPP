using AIMLAPP.Configuration;
using AIMLAPP.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace AIMLAPP.Mcp;

// Menu option 4, or `dotnet run -- --mcp`. Exposes ExperienceMcpTools to MCP clients over stdio.
// Guide: Mcp/04-McpServer.md. The video explains what MCP is; this is the C# server behind it:
// https://www.youtube.com/watch?v=lEUPgdv0gY8
public static class McpServerRunner
{
    public static async Task RunAsync(string openAiApiKey, CancellationToken cancellationToken = default)
    {
        // NOTE: MCP over stdio uses stdout for JSON-RPC. Nothing must ever write
        // to Console.Out from this process while the server is running - route
        // all logs to stderr.
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Logging.AddConsole(o =>
        {
            o.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer(AppDbContext.ConnectionString));

        builder.Services.AddSingleton(_ => new EmbeddingClient(AppSettings.Current.OpenAI.EmbeddingModel, openAiApiKey));

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();
        await host.RunAsync(cancellationToken);
    }
}
