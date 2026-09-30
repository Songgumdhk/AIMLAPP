using System.Text;
using OpenAI.Chat;
using OpenAI.Embeddings;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.ObservabilityDemo.Tracing;

namespace AIMLAPP.Learning.ObservabilityDemo.Pipeline;

// The chapter's request path with a span around every step that can fail or cost money:
// request → embed.query → retrieve → tool.lookup_policy → chat.answer.
// See 06-Observability.md §1-2 for the tree this produces.
public static class ObservedPipeline
{
    public const string DefaultQuestion = "How many paid vacation days does a new hire get?";

    public static async Task<string> AnswerAsync(
        Tracer tracer,
        ChatClient chat,
        EmbeddingClient embeddings,
        Handbook handbook,
        string question)
    {
        // Parent span. It does not call a model, so it stores no tokens.
        using var request = tracer.Begin("request", "server");
        request.SetDetail(question);

        // STEP 1: Embed the question. A billable API call, so it gets a model and tokens.
        float[] vector;
        using (var embed = tracer.Begin("embed.query", "embed"))
        {
            embed.SetPromptPreview(question);
            var response = await embeddings.GenerateEmbeddingsAsync([question]);
            var usage = response.Value.Usage;
            embed.RecordEmbed(ModelNames.Embed, usage?.InputTokenCount ?? 0);
            // Batch results are matched by Index, not by list position.
            vector = response.Value.OrderBy(e => e.Index).First().ToFloats().ToArray();
            embed.SetDetail("1 query vector");
        }

        // STEP 2: Local search. No Model is set, so this span costs $0 and has no tokens.
        // The retrieved ids go in Detail so you can see what the model was shown.
        List<HandbookHit> hits;
        using (var retrieve = tracer.Begin("retrieve", "internal"))
        {
            hits = handbook.Search(vector, 2);
            retrieve.SetDetail(string.Join(", ", hits.Select(h => h.Id)));
            retrieve.SetOutputPreview(string.Join(" | ", hits.Select(h => h.Title)));
        }

        // STEP 3: Local tool, same idea as chapter 1: your code runs it, the trace records it.
        // If a tool fails, call Fail() on its span. The request can still succeed,
        // and both statuses stay visible (see SampleErrorTrace in ObservabilityDemo).
        string toolText;
        using (var tool = tracer.Begin("tool.lookup_policy", "tool"))
        {
            var top = hits[0];
            toolText = $"{top.Id}: {top.Body}";
            tool.SetDetail(top.Id);
            tool.SetOutputPreview(toolText);
        }

        var context = BuildContext(hits, toolText);
        var prompt = $"HANDBOOK:\n{context}\n\nQUESTION: {question}";

        // STEP 4: The chat call. Usually the slowest and most expensive span (§4).
        using var answer = tracer.Begin("chat.answer", "llm");
        answer.SetPromptPreview(prompt);
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "Answer in at most two sentences. Use only the handbook excerpts. " +
                    "If they do not contain the answer, say you don't know."),
                new UserChatMessage(prompt),
            };
            var options = new ChatCompletionOptions
            {
                Temperature = AppSettings.Current.OpenAI.Temperature,
                // Caps the output side so a long answer cannot inflate the bill.
                // Tune it in appsettings.json → Observability.AnswerMaxOutputTokens.
                MaxOutputTokenCount = AppSettings.Current.Observability.AnswerMaxOutputTokens,
            };
            var completion = await chat.CompleteChatAsync(messages, options);
            // Use the usage the API reports. Counting tokens yourself drifts from the bill.
            var usage = completion.Value.Usage;
            // Cached tokens are a subset of input, already included in InputTokenCount.
            var cached = usage?.InputTokenDetails?.CachedTokenCount ?? 0;
            answer.RecordChat(
                ModelNames.Chat,
                usage?.InputTokenCount ?? 0,
                usage?.OutputTokenCount ?? 0,
                cached);
            var text = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text ?? ""
                : "";
            answer.SetOutputPreview(text);
            answer.SetDetail(ModelNames.Chat);
            return text;
        }
        catch (Exception ex)
        {
            // Record the failure on the span, then let the caller see the exception.
            answer.Fail(ex.Message);
            throw;
        }
    }

    // Two chat calls, same question. The second prompt is padded so input
    // tokens move while the question stays the same. Shows that stuffed
    // context, not answer length, is often what drives the bill (§5).
    public static async Task ComparePromptLengthAsync(Tracer tracer, ChatClient chat)
    {
        const string question = "In one sentence, what is 2+2?";
        // Repeated so the long prompt is obviously bigger. The question itself does not change.
        var filler = string.Join(
            "\n",
            Enumerable.Repeat("Acme note: attach the receipt when the meal is over $25.", 200));

        await CompleteAsync(tracer, chat, "chat.short", question);
        await CompleteAsync(tracer, chat, "chat.long", question + "\n\nHANDBOOK:\n" + filler);
    }

    private static async Task CompleteAsync(Tracer tracer, ChatClient chat, string spanName, string prompt)
    {
        using var span = tracer.Begin(spanName, "llm");
        span.SetPromptPreview(prompt);
        span.SetDetail($"{prompt.Length} characters");
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("Reply in one short sentence."),
            new UserChatMessage(prompt),
        };
        var options = new ChatCompletionOptions
        {
            Temperature = AppSettings.Current.OpenAI.Temperature,
            // A small cap keeps output nearly equal on both calls, so only input moves.
            MaxOutputTokenCount = AppSettings.Current.Observability.CompareMaxOutputTokens,
        };
        var completion = await chat.CompleteChatAsync(messages, options);
        var usage = completion.Value.Usage;
        span.RecordChat(
            ModelNames.Chat,
            usage?.InputTokenCount ?? 0,
            usage?.OutputTokenCount ?? 0,
            usage?.InputTokenDetails?.CachedTokenCount ?? 0);
        var text = completion.Value.Content.Count > 0
            ? completion.Value.Content[0].Text ?? ""
            : "";
        span.SetOutputPreview(text);
    }

    // Everything here becomes input tokens on chat.answer. More context, bigger bill.
    private static string BuildContext(IReadOnlyList<HandbookHit> hits, string toolText)
    {
        var sb = new StringBuilder();
        foreach (var hit in hits)
        {
            sb.Append('[').Append(hit.Id).Append("] ").AppendLine(hit.Title);
            sb.AppendLine(hit.Body);
        }

        sb.AppendLine();
        sb.Append("Tool result: ").AppendLine(toolText);
        return sb.ToString();
    }
}
