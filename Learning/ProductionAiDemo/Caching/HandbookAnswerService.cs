using OpenAI.Chat;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.ProductionAiDemo.Deployment;
using AIMLAPP.Learning.ProductionAiDemo.Pipeline;
using AIMLAPP.Learning.ProductionAiDemo.RateLimit;

namespace AIMLAPP.Learning.ProductionAiDemo.Caching;

// The production shape of a cached answer.
// An API endpoint would call AskAsync. It would not call the model itself.
// WHY: every request then goes through the same SafePipeline gates. No route can skip them.
public sealed class HandbookAnswerService
{
    private readonly ChatClient _chat;
    private readonly AnswerCache _cache;
    private readonly WindowLimiter _limiter;
    private readonly TimeSpan _ttl;

    public HandbookAnswerService(ChatClient chat, AnswerCache cache, WindowLimiter limiter, TimeSpan ttl)
    {
        _chat = chat;
        _cache = cache;
        _limiter = limiter;
        _ttl = ttl;
    }

    // Counts real model calls. A cache hit must leave this number unchanged.
    public int ModelCalls { get; private set; }

    public Task<PipelineResult> AskAsync(string question, DateTimeOffset now) =>
        SafePipeline.RunAsync(
            question,
            _cache,
            _limiter,
            now,
            _ttl,
            handbook => GenerateAsync(handbook, question),
            fromModel: true);

    private async Task<string> GenerateAsync(string handbook, string question)
    {
        ModelCalls++;

        var messages = new List<ChatMessage>
        {
            // The prompt asks for the [doc-id] citation and the "don't know" refusal
            // that OutputGuard checks for. Prompt and guard must agree.
            new SystemChatMessage(
                "Answer in one or two sentences using only HANDBOOK. " +
                "Cite the document id in brackets, like [hr-vacation]. " +
                "If the handbook does not contain the answer, say you don't know."),
            new UserChatMessage($"HANDBOOK:\n{handbook}\n\nQUESTION: {question}"),
        };
        var options = new ChatCompletionOptions
        {
            Temperature = AppSettings.Current.OpenAI.Temperature,
            // Output cap from appsettings.json → ProductionAi.MaxOutputTokens.
            MaxOutputTokenCount = AppConfig.Demo.MaxOutputTokens,
        };
        var completion = await _chat.CompleteChatAsync(messages, options);
        return completion.Value.Content.Count > 0
            ? completion.Value.Content[0].Text ?? ""
            : "";
    }
}
