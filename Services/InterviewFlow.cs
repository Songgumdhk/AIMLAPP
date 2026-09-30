using AIMLAPP.Configuration;
using AIMLAPP.Data;
using AIMLAPP.Models;
using Microsoft.Agents.AI;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace AIMLAPP.Services;

// Menu option 3. RAG in three steps: embed the input, find the closest profile, generate questions.
// Guide: Services/03-InterviewFlow.md. Video walkthrough ("agent + RAG"):
// https://www.youtube.com/watch?v=lEUPgdv0gY8
public static class InterviewFlow
{
    private static string EmbeddingModel => AppSettings.Current.OpenAI.EmbeddingModel;
    private static string ChatModel => AppSettings.Current.OpenAI.ChatModel;

    public static async Task RunAsync(string openAiApiKey)
    {
        var embeddingClient = new EmbeddingClient(EmbeddingModel, openAiApiKey);
        await using var db = new AppDbContext();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Describe your experience / skill level (type 'exit' to quit):");
            Console.Write("> ");
            var userExperience = Console.ReadLine();

            if (userExperience is null)
                break;

            if (string.IsNullOrWhiteSpace(userExperience))
                continue;

            if (userExperience.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Bye.");
                break;
            }

            // 1. Embed the user's input with the same model used at seed time.
            Console.WriteLine("\nGenerating embedding...");
            var embedding = await embeddingClient.GenerateEmbeddingAsync(userExperience);
            var userVector = new SqlVector<float>(embedding.Value.ToFloats());

            // 2. Ask SQL Server for the closest ExpVector using cosine distance.
            Console.WriteLine("Searching for closest matching experience...");
            var match = await db.Experiences
                .AsNoTracking()
                .OrderBy(e => EF.Functions.VectorDistance("cosine", e.ExpVector!.Value, userVector))
                .FirstOrDefaultAsync();

            if (match is null || string.IsNullOrWhiteSpace(match.Questions))
            {
                Console.WriteLine("No matching experience with questions was found.");
                continue;
            }

            Console.WriteLine();
            Console.WriteLine($"Matched profile : {match.ExperienceText}");
            Console.WriteLine(new string('-', 60));

            // 3. Hand the matched questions to an agent that lists them.
            await RunInterviewAgentAsync(openAiApiKey, match);

            Console.WriteLine();
            Console.WriteLine(new string('=', 60));
        }
    }

    private static async Task RunInterviewAgentAsync(string openAiApiKey, Experience match)
    {
        IChatClient chatClient = new ChatClient(ChatModel, openAiApiKey).AsIChatClient();

        var instructions = $"""
                    You are a technical interviewer. Based on the matched candidate profile
                    and the interview questions listed below, generate exactly 5 technical
                    interview questions that should be asked to the candidate.

                    The questions should be appropriate for the candidate's experience level
                    and relevant to the source questions. You may adapt or refine the source
                    questions when necessary.

                    Do not ask for answers, do not add commentary, greetings, explanations,
                    or a closing statement.

                    Output exactly 5 questions as a clean numbered list.

                    Matched candidate profile: {match.ExperienceText}

                    Source questions:
                    {match.Questions}
                    """;

        AIAgent agent = chatClient.AsAIAgent(name: "Interviewer", instructions: instructions);

        Console.WriteLine("Interview questions:");
        Console.WriteLine();

        await foreach (var update in agent.RunStreamingAsync("List the interview questions now."))
        {
            Console.Write(update);
        }
        Console.WriteLine();
    }
}
