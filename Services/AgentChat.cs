using AIMLAPP.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace AIMLAPP.Services;

// Menu option 1. The smallest possible agent: one model, one instruction, streaming output.
// Guide: Services/01-ChatWithAgent.md. Video walkthrough ("mock interview agent"):
// https://www.youtube.com/watch?v=lEUPgdv0gY8
public static class AgentChat
{
    public static async Task RunAsync(string openAiApiKey)
    {
        // IChatClient is the provider-neutral interface from Microsoft.Extensions.AI.
        // Swapping OpenAI for Azure OpenAI or Ollama only changes this line.
        IChatClient chatClient = new ChatClient(AppSettings.Current.OpenAI.ChatModel, openAiApiKey).AsIChatClient();

        AIAgent agent = chatClient.AsAIAgent(
            name: "MyAgent",
            instructions: "Send only 3 interview questions as per the skill put by the developer and its timebound for 1 minute"
        );

        while (true)
        {
            Console.WriteLine("Enter who you are (type 'exit' to quit):");
            var userPrompt = Console.ReadLine();

            if (userPrompt is null)
                break;

            if (string.IsNullOrWhiteSpace(userPrompt))
                continue;

            if (userPrompt.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            Console.WriteLine($"\nUser: {userPrompt}\n");
            Console.Write("Agent: ");

            // No session is passed, so every turn starts fresh: the agent does not remember
            // earlier turns. The guide shows how to add memory with CreateSessionAsync.
            await foreach (var update in agent.RunStreamingAsync(userPrompt))
            {
                Console.Write(update);
            }

            Console.WriteLine("\n");
            Console.WriteLine(new string('-', 60));
        }
    }
}
