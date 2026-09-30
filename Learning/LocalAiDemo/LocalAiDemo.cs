// =============================================================================
//  Chapter 8 — Local AI Demo (entry point)
// =============================================================================
//
//  PURPOSE
//  -------
//  Show the four pieces from the roadmap's Local AI section:
//  Ollama, ONNX, local embeddings, local LLMs.
//
//  Folder map (matches 08-LocalAI.md):
//    Ollama/      OllamaClient
//    Onnx/        LocalWeightModel
//    Embeddings/  BagEmbedder, LocalCorpus, VectorStore
//    Llm/         ModelCards, ExtractiveAnswerer
//    Checks/      LocalAiChecks
//
//  HOW TO RUN
//  ----------
//  From Program.cs, menu option 12.
//  Ollama is optional. The bag-of-words embedder and the tiny ONNX-style model
//  always run; option 3 also uses nomic-embed-text when Ollama has it installed.
// =============================================================================

using System.Globalization;
using System.Text;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.LocalAiDemo.Checks;
using AIMLAPP.Learning.LocalAiDemo.Embeddings;
using AIMLAPP.Learning.LocalAiDemo.Llm;
using AIMLAPP.Learning.LocalAiDemo.Ollama;
using AIMLAPP.Learning.LocalAiDemo.Onnx;

namespace AIMLAPP.Learning.LocalAiDemo;

// Menu + orchestration for Chapter 8. Each Show* method is one section of the guide.
// Nothing here needs an OpenAI key; every model call goes to this machine.
public static class LocalAiDemo
{
    // Preferred model names from appsettings.json → Ollama:ChatModel / Ollama:EmbeddingModel.
    // Two different files: a chat model writes text, an embedding model turns text into vectors.
    private static string ChatModel => AppSettings.Current.Ollama.ChatModel;
    private static string EmbeddingModel => AppSettings.Current.Ollama.EmbeddingModel;

    public static async Task RunAsync()
    {
        Console.OutputEncoding = Encoding.UTF8;
        // Fail fast if the offline examples drifted or Ollama:BaseUrl isn't loopback.
        LocalAiChecks.Verify();

        using var ollama = new OllamaClient();

        Console.WriteLine("=== Local AI Demo ===");
        Console.WriteLine("Read 08-LocalAI.md alongside the menu.");
        Console.WriteLine("Option 2 never calls the network. Option 3 adds Ollama on 127.0.0.1 if it is running.");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- Pick a piece ---");
            Console.WriteLine("  1) Ollama            — is the local server up, and which models are installed?");
            Console.WriteLine("  2) ONNX              — run a tiny model from a file on this CPU");
            Console.WriteLine($"  3) Local embeddings  — bag-of-words vs {EmbeddingModel}, no OpenAI call");
            Console.WriteLine("  4) Local LLMs        — pick a GGUF or ONNX model, then answer locally");
            Console.WriteLine($"  5) Chat              — talk to {ChatModel} on this machine");
            Console.WriteLine("  0) Quit");
            Console.Write("Selection: ");
            var mode = Console.ReadLine()?.Trim();

            switch (mode)
            {
                case "0":
                    return;
                case "1":
                    await ShowOllamaAsync(ollama);
                    break;
                case "2":
                    ShowOnnx();
                    break;
                case "3":
                    await ShowEmbeddingsAsync(ollama);
                    break;
                case "4":
                    await ShowLocalLlmAsync(ollama);
                    break;
                case "5":
                    await ChatWithOllamaAsync(ollama);
                    break;
                default:
                    Console.WriteLine("Pick a number from the menu.");
                    break;
            }
        }
    }

    private static async Task ShowOllamaAsync(OllamaClient ollama)
    {
        Console.WriteLine();
        Console.WriteLine($"Looking for Ollama at {OllamaClient.DefaultBaseUrl}");
        if (!await ollama.IsReachableAsync())
        {
            Console.WriteLine("Ollama is not running. The rest of this chapter still works.");
            Console.WriteLine("Install it from https://ollama.com, then:");
            Console.WriteLine($"  ollama pull {ChatModel}");
            Console.WriteLine($"  ollama pull {EmbeddingModel}");
            return;
        }

        var models = await ollama.ListModelsAsync();
        Console.WriteLine(models.Count == 0
            ? "The server is up, and no models are installed yet."
            : "Installed models:");
        foreach (var name in models)
            Console.WriteLine("  " + name);

        // Naming heuristic: embedding models have "embed" in the name. Anything else is
        // treated as a chat model, since embedders can't generate text.
        var chatModel = models.FirstOrDefault(name => !name.Contains("embed", StringComparison.OrdinalIgnoreCase));
        if (chatModel is null)
        {
            Console.WriteLine($"No chat model yet. Run: ollama pull {ChatModel}");
            return;
        }

        Console.Write($"Ask {chatModel} one local question? [y/n]: ");
        if (Console.ReadLine()?.Trim().ToLowerInvariant() is not ("y" or "yes"))
            return;

        Console.Write("Question (Enter = Say hello in five words.): ");
        var typed = Console.ReadLine();
        var prompt = string.IsNullOrWhiteSpace(typed) ? "Say hello in five words." : typed.Trim();
        Console.WriteLine(await ollama.ChatAsync(chatModel, prompt));
    }

    private static async Task ChatWithOllamaAsync(OllamaClient ollama)
    {
        Console.WriteLine();
        if (!await ollama.IsReachableAsync())
        {
            Console.WriteLine("Ollama is not running. Start the Ollama app, then try again.");
            return;
        }

        // Prefer the configured model; StartsWith matches the ":latest" tag Ollama adds.
        // Fall back to any installed chat model so the demo still runs.
        var models = await ollama.ListModelsAsync();
        var chatModel = models.FirstOrDefault(name => name.StartsWith(ChatModel, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(name => !name.Contains("embed", StringComparison.OrdinalIgnoreCase));
        if (chatModel is null)
        {
            Console.WriteLine($"No chat model installed. Run: ollama pull {ChatModel}");
            return;
        }

        // The app owns the conversation memory, not Ollama.
        var history = new List<OllamaMessage>
        {
            new("system", "You are a helpful assistant running locally. Keep answers clear and not longer than needed."),
        };

        Console.WriteLine($"Chat with {chatModel}. Words appear as Ollama sends them. Type exit to leave.");
        while (true)
        {
            Console.Write("You: ");
            var text = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            if (text.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                return;

            // CRITICAL: Ollama is stateless. The WHOLE history is resent every turn, which is
            // how the model "remembers" earlier turns. Longer chats = bigger prompts = slower.
            history.Add(new OllamaMessage("user", text.Trim()));
            Console.Write("Assistant: ");
            // Console.Write is the onChunk callback: tokens print as they stream in.
            var reply = await ollama.ChatStreamAsync(chatModel, history, Console.Write);
            Console.WriteLine();
            Console.WriteLine();
            // Save the model's reply too, or the next turn would lose its own answer.
            history.Add(new OllamaMessage("assistant", reply));
        }
    }

    // §3: run inference in memory, save the weights, load them back, run again.
    // Identical outputs prove the file alone is enough to reproduce the model.
    private static void ShowOnnx()
    {
        var input = new[] { 3f, 4f };
        var output = LocalWeightModel.TinyLinear.Predict(input);

        var path = Path.Combine(AppContext.BaseDirectory, "tiny-linear.json");
        LocalWeightModel.TinyLinear.Save(path);
        var loaded = LocalWeightModel.Load(path);
        var fromFile = loaded.Predict(input);

        Console.WriteLine();
        Console.WriteLine("Local inference: y = Wx + b, loaded from a file, run on this CPU.");
        Console.WriteLine($"  input   [{input[0]}, {input[1]}]");
        Console.WriteLine($"  output  [{fromFile[0].ToString(CultureInfo.InvariantCulture)}, {fromFile[1].ToString(CultureInfo.InvariantCulture)}]");
        Console.WriteLine($"  file    {path}");
        Console.WriteLine("  in memory this was " +
            $"[{output[0].ToString(CultureInfo.InvariantCulture)}, {output[1].ToString(CultureInfo.InvariantCulture)}]");
        Console.WriteLine();
        Console.WriteLine("A real ONNX model is the same three steps with a different loader:");
        Console.WriteLine("  using var session = new InferenceSession(\"model.onnx\");");
        Console.WriteLine("  var results = session.Run(inputs);");
        Console.WriteLine("Ollama does the same job for a .gguf file. Q4 means the weights are stored in about 4 bits.");
        Console.WriteLine("CPU is the default. A GPU execution provider is the same Run call on a different device.");
    }

    // §4: rank the same documents with two local embedders side by side.
    // Scores in the Bag and Ollama columns are each only comparable within their own column.
    private static async Task ShowEmbeddingsAsync(OllamaClient ollama)
    {
        var embedder = new BagEmbedder(LocalCorpus.Docs.Select(doc => doc.Text));
        const string defaultQuestion = "how many paid vacation days";

        Console.WriteLine();
        Console.Write($"Question (Enter = {defaultQuestion}): ");
        var typed = Console.ReadLine();
        var question = string.IsNullOrWhiteSpace(typed) ? defaultQuestion : typed.Trim();
        // Question and documents go through the SAME embedder, so their vectors are comparable.
        var query = embedder.Embed(question);
        var bagScores = LocalCorpus.Docs
            .Select(doc => BagEmbedder.Cosine(query, embedder.Embed(doc.Text)))
            .ToArray();

        var ollamaScores = await TryOllamaScoresAsync(ollama, question);

        Console.WriteLine();
        Console.WriteLine($"Bag-of-words: {embedder.Dimensions} dimensions, built from these documents.");
        if (ollamaScores is not null)
            Console.WriteLine($"Ollama:       {ollamaScores.Value.Model}, {ollamaScores.Value.Dimensions} dimensions.");
        Console.WriteLine("Question: " + question);
        Console.WriteLine();
        Console.WriteLine(ollamaScores is null
            ? $"{"Document",-16} {"Bag",8}"
            : $"{"Document",-16} {"Bag",8} {"Ollama",8}");

        for (var i = 0; i < LocalCorpus.Docs.Count; i++)
        {
            var line = $"{LocalCorpus.Docs[i].Id,-16} {bagScores[i].ToString("0.00", CultureInfo.InvariantCulture),8}";
            if (ollamaScores is not null)
                line += $" {ollamaScores.Value.Scores[i].ToString("0.00", CultureInfo.InvariantCulture),8}";
            Console.WriteLine(line);
        }

        Console.WriteLine();
        Console.WriteLine("Nearest (bag):    " + (bagScores.Max() > 0
            ? LocalCorpus.Docs[Array.IndexOf(bagScores, bagScores.Max())].Id
            : "none — no shared words"));
        if (ollamaScores is not null)
        {
            var scores = ollamaScores.Value.Scores;
            Console.WriteLine("Nearest (Ollama): " + LocalCorpus.Docs[Array.IndexOf(scores, scores.Max())].Id);
            Console.WriteLine("Try a question with no shared words, e.g. \"holiday leave\" — only the neural model still finds it.");
        }

        Console.WriteLine("Embed the documents and the question with this same local model.");
        Console.WriteLine("A vector from OpenAI cannot be compared with a vector from this model.");
    }

    private static async Task<(string Model, int Dimensions, double[] Scores)?> TryOllamaScoresAsync(
        OllamaClient ollama, string question)
    {
        if (!await ollama.IsReachableAsync())
        {
            Console.WriteLine("Ollama is not running, so only the bag-of-words embedder is used.");
            return null;
        }

        var models = await ollama.ListModelsAsync();
        var embedModel = models.FirstOrDefault(name => name.StartsWith(EmbeddingModel, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(name => name.Contains("embed", StringComparison.OrdinalIgnoreCase));
        if (embedModel is null)
        {
            Console.WriteLine($"No embedding model installed. Run: ollama pull {EmbeddingModel}");
            return null;
        }

        // nomic-embed-text was trained with these task prefixes; without them retrieval scores drop.
        // WHY two prefixes: a question and the passage that answers it are worded differently.
        // "search_query:" and "search_document:" tell the model which side each text is on,
        // so it places them close together. Other models (e.g. MiniLM) take no prefix.
        var isNomic = embedModel.StartsWith("nomic-embed-text", StringComparison.OrdinalIgnoreCase);
        var queryText = isNomic ? "search_query: " + question : question;

        // Document vectors are cached in a JSON file keyed by model name (see VectorStore).
        // Only new or edited documents are sent to Ollama; the question is embedded every time.
        var path = Path.Combine(AppContext.BaseDirectory, "local-vectors.json");
        var store = VectorStore.Open(path, embedModel);
        var missing = LocalCorpus.Docs.Where(doc => store.Find(doc.Id, doc.Text) is null).ToArray();
        if (missing.Length > 0)
        {
            var texts = missing.Select(doc => isNomic ? "search_document: " + doc.Text : doc.Text).ToArray();
            var vectors = await ollama.EmbedAsync(embedModel, texts);
            for (var i = 0; i < missing.Length; i++)
                store.Upsert(missing[i].Id, missing[i].Text, vectors[i]);
            store.Save(path);
        }

        var reused = LocalCorpus.Docs.Count - missing.Length;
        Console.WriteLine($"Document vectors: {reused} loaded from file, {missing.Length} embedded and saved.");
        Console.WriteLine($"  file {path}");

        var docVectors = LocalCorpus.Docs.Select(doc => store.Find(doc.Id, doc.Text)!).ToArray();
        var queryVector = await ollama.EmbedAsync(embedModel, queryText);
        var scores = docVectors.Select(vector => BagEmbedder.Cosine(queryVector, vector)).ToArray();
        return (embedModel, queryVector.Length, scores);
    }

    // §5: local RAG. Retrieve the nearest document locally, answer extractively (no LLM),
    // then optionally let a local LLM rephrase that same document into one sentence.
    private static async Task ShowLocalLlmAsync(OllamaClient ollama)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Model",-20} {"File",-8} {"Quant",-8} {"Memory",-14} Use");
        foreach (var card in ModelCards.All)
            Console.WriteLine($"{card.Name,-20} {card.FileFormat,-8} {card.Quantization,-8} {card.Memory,-14} {card.Use}");

        const string defaultQuestion = "how many paid vacation days";
        Console.WriteLine();
        Console.Write($"Question (Enter = {defaultQuestion}): ");
        var typed = Console.ReadLine();
        var question = string.IsNullOrWhiteSpace(typed) ? defaultQuestion : typed.Trim();

        // STEP 1: Retrieve. Prefer the neural embedder (matches meaning); fall back to
        // bag-of-words (matches shared words) when Ollama or its embedding model is missing.
        LocalDoc doc;
        double score;
        string retriever;
        if (await TryOllamaScoresAsync(ollama, question) is { } found)
        {
            var bestIndex = Array.IndexOf(found.Scores, found.Scores.Max());
            (doc, score, retriever) = (LocalCorpus.Docs[bestIndex], found.Scores[bestIndex], found.Model);
        }
        else
        {
            var embedder = new BagEmbedder(LocalCorpus.Docs.Select(item => item.Text));
            (doc, score) = ExtractiveAnswerer.Best(embedder, question);
            retriever = "bag-of-words";
        }

        // STEP 2: Extractive answer. Returning the document verbatim can't hallucinate,
        // but it also can't rephrase or combine facts.
        Console.WriteLine();
        Console.WriteLine("No generator installed is still an answer: return the nearest local document.");
        Console.WriteLine($"  retrieved by {retriever}, score {score.ToString("0.00", CultureInfo.InvariantCulture)}");
        Console.WriteLine("  " + ExtractiveAnswerer.Answer(doc));

        if (!await ollama.IsReachableAsync())
        {
            Console.WriteLine();
            Console.WriteLine("Ollama is not running, so the generative step stays on this extractive answer.");
            return;
        }

        var models = await ollama.ListModelsAsync();
        var chatModel = models.FirstOrDefault(name => name.StartsWith(ChatModel, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(name => !name.Contains("embed", StringComparison.OrdinalIgnoreCase));
        if (chatModel is null)
        {
            Console.WriteLine();
            Console.WriteLine($"Install a chat model with: ollama pull {ChatModel}");
            return;
        }

        Console.WriteLine();
        Console.Write($"Ask local model {chatModel} to answer from that document only? [y/n]: ");
        if (Console.ReadLine()?.Trim().ToLowerInvariant() is not ("y" or "yes"))
            return;

        // STEP 3: Generative answer, grounded the same way as Chapter 3's AnswerGenerator:
        // context in the prompt, "only this document", cite the id so the user can verify.
        // Local weights don't make this safe by default: a poisoned document pasted here
        // would still steer the model (Chapter 7 guards still apply).
        var prompt =
            "Answer in one sentence using only this document. Cite the id in brackets.\n" +
            $"[{doc.Id}] {doc.Text}\nQuestion: {question}";
        Console.WriteLine(await ollama.ChatAsync(chatModel, prompt));
    }
}
