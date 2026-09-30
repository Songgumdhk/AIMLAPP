using AIMLAPP.Configuration;
using AIMLAPP.ConsoleUi;
using AIMLAPP.Learning.AdvancedRagDemo;
using AIMLAPP.Learning.AgenticWorkflowsDemo;
using AIMLAPP.Learning.AiEvaluationDemo;
using AIMLAPP.Learning.FunctionCallingDemo;
using AIMLAPP.Learning.LocalAiDemo;
using AIMLAPP.Learning.ObservabilityDemo;
using AIMLAPP.Learning.ProductionAiDemo;
using AIMLAPP.Learning.SemanticKernelDemo;
using AIMLAPP.Mcp;
using AIMLAPP.Services;

var settings = AppSettings.Current;

// MCP mode: no menu, no stdout - the client owns stdin/stdout for JSON-RPC.
if (args.Contains("--mcp"))
{
    await McpServerRunner.RunAsync(settings.RequireOpenAiApiKey());
    return;
}

AppBanner.PrintMenu();
var choice = Console.ReadLine()?.Trim();

var apiKey = settings.RequireOpenAiApiKey();

if (choice == "2")
{
    await ExperienceSeeder.SeedAsync(apiKey);
    return;
}

if (choice == "3")
{
    await InterviewFlow.RunAsync(apiKey);
    return;
}

if (choice == "4")
{
    await McpServerRunner.RunAsync(apiKey);
    return;
}

if (choice == "5")
{
    await FunctionCallingDemo.RunAsync(apiKey);
    return;
}

if (choice == "6")
{
    await SemanticKernelDemo.RunAsync(apiKey);
    return;
}

if (choice == "7")
{
    await AdvancedRagDemo.RunAsync(apiKey);
    return;
}

if (choice == "8")
{
    await AgenticWorkflowsDemo.RunAsync(apiKey);
    return;
}

if (choice == "9")
{
    await AiEvaluationDemo.RunAsync(apiKey);
    return;
}

if (choice == "10")
{
    await ObservabilityDemo.RunAsync(apiKey);
    return;
}

if (choice == "11")
{
    await ProductionAiDemo.RunAsync(apiKey);
    return;
}

// Local AI runs without an OpenAI key.
if (choice == "12")
{
    await LocalAiDemo.RunAsync();
    return;
}


// Option 1, and the default for any other input.
await AgentChat.RunAsync(apiKey);
