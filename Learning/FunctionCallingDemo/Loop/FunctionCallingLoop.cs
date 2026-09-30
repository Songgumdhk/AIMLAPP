using OpenAI.Chat;

namespace AIMLAPP.Learning.FunctionCallingDemo.Loop;

// THE FUNCTION-CALLING LOOP — the heart of every agent.
// Send messages + tools → LLM requests tools → execute → feed results back → repeat
// until FinishReason == Stop or MAX_TURNS is hit.
// This is exactly what SK's FunctionChoiceBehavior.Auto() automates in Chapter 2.
// See 01-FunctionCalling.md §2 (the loop) and §6 (sequential chains).
public static class FunctionCallingLoop
{
    public static async Task RunTurnAsync(
        ChatClient client,
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        int maxTurns)
    {
        bool completed = false;
        // WHY a cap: a broken tool or confused model can request tools forever,
        // and every turn is a paid API call (§9). One "turn" = one LLM round-trip.
        for (int turn = 0; turn < maxTurns; turn++)
        {
            // STEP 1: Send the entire conversation + tool list to the LLM.
            // The model is stateless: it only "remembers" earlier tool results
            // because we resend the full message list every time.
            ChatCompletion completion = await client.CompleteChatAsync(messages, options);

            // STEP 2: Inspect what the LLM decided.
            //   - Stop         => it produced a final text answer, we're done.
            //   - ToolCalls    => it wants us to run 1+ tools.
            if (completion.FinishReason == ChatFinishReason.Stop)
            {
                var answer = completion.Content.Count > 0 ? completion.Content[0].Text : "(no content)";
                Console.WriteLine($"\nAssistant: {answer}");
                // Keep the final answer in history so the next user turn has context.
                messages.Add(new AssistantChatMessage(completion));
                completed = true;
                break;
            }

            // Other finish reasons (e.g. Length = hit the token limit, ContentFilter)
            // mean there is neither a clean answer nor tools to run, so stop here.
            if (completion.FinishReason != ChatFinishReason.ToolCalls)
            {
                Console.WriteLine($"\n[Unexpected finish reason: {completion.FinishReason}]");
                completed = true;
                break;
            }

            // CRITICAL: add the assistant message (tool-call requests) BEFORE
            // adding tool results, or the API will reject them.
            messages.Add(new AssistantChatMessage(completion));

            Console.WriteLine($"\n[Turn {turn + 1}/{maxTurns}] LLM requested {completion.ToolCalls.Count} tool call(s)");

            // STEP 3: Execute ALL requested tool calls IN PARALLEL.
            // One response can hold several independent calls (§7). Handle every
            // one, not just the first. Task.WhenAll is safe here because
            // ToolDispatcher never throws; failures come back as strings.
            var executionTasks = completion.ToolCalls
                .Select(ToolDispatcher.ExecuteAsync)
                .ToList();

            var toolResults = await Task.WhenAll(executionTasks);

            // STEP 4: Feed every result back. Each ToolChatMessage MUST
            // reference the exact ToolCallId from the assistant message.
            foreach (var (toolCall, result) in completion.ToolCalls.Zip(toolResults))
            {
                Console.WriteLine($"  -> {toolCall.FunctionName}({toolCall.FunctionArguments}) = {result}");
                messages.Add(new ToolChatMessage(toolCall.Id, result));
            }
        }

        // Fail loudly when the cap trips, so it is obvious the answer is missing
        // rather than silently returning nothing (Exercise #5).
        if (!completed)
        {
            Console.WriteLine();
            Console.WriteLine($"[!!] MAX_TURNS ({maxTurns}) reached without a final answer.");
            Console.WriteLine("     The LLM is either stuck in a loop or needs more turns.");
            Console.WriteLine("     Restart the demo with a higher MAX_TURNS to see it succeed.");
        }
    }
}
