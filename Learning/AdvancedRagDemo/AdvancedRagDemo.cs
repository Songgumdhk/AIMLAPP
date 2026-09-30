// =============================================================================
//  Chapter 3 — Advanced RAG Demo (entry point)
// =============================================================================
//
//  PURPOSE
//  -------
//  Orchestrate the RAG pipeline: chunk → embed → store → retrieve → generate.
//  Each step lives in its own class under Learning/AdvancedRagDemo/.
//
//  Folder map (matches 03-AdvancedRAG.md):
//    Models/       SourceDoc, Chunk, ScoredChunk
//    Chunking/     Chunkers (§4)
//    Embeddings/   EmbeddingHelper (§5)
//    Store/        InMemoryVectorStore (§5, §6, §9)
//    Retrieval/    Rrf (§7), Reranker (§8), QueryRewriter (§10), RagRetriever
//    Generation/   AnswerGenerator (the "G" in RAG)
//    Data/         SampleCorpus
//    ConsoleUi/    ResultPrinter
//
//  HOW TO RUN
//  ----------
//  From Program.cs, menu option 7.
// =============================================================================

using System.Text;
using OpenAI.Chat;
using OpenAI.Embeddings;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.AdvancedRagDemo.Chunking;
using AIMLAPP.Learning.AdvancedRagDemo.ConsoleUi;
using AIMLAPP.Learning.AdvancedRagDemo.Data;
using AIMLAPP.Learning.AdvancedRagDemo.Embeddings;
using AIMLAPP.Learning.AdvancedRagDemo.Generation;
using AIMLAPP.Learning.AdvancedRagDemo.Models;
using AIMLAPP.Learning.AdvancedRagDemo.Retrieval;
using AIMLAPP.Learning.AdvancedRagDemo.Store;

namespace AIMLAPP.Learning.AdvancedRagDemo;

public static class AdvancedRagDemo
{
    public static async Task RunAsync(string apiKey)
    {
        // Force UTF-8 output so the LLM's occasional accented characters don't
        // corrupt the console (same fix as Chapters 1 & 2).
        Console.OutputEncoding = Encoding.UTF8;

        var settings = AppSettings.Current;
        var rag = settings.Rag;

        var chatClient = new ChatClient(settings.OpenAI.ChatModel, apiKey);
        // text-embedding-3-small is cheap (~$0.02 per 1M tokens), 1536-dim, fast.
        // In production for large corpora, consider `text-embedding-3-large`.
        // Set in appsettings.json → OpenAI:EmbeddingModel. If you change it, re-ingest:
        // query and chunk vectors must come from the same model.
        var embeddingClient = new EmbeddingClient(settings.OpenAI.EmbeddingModel, apiKey);

        // STEP 1 — Ask the user which chunking strategy to use.
        // This is Exercise #1 from the theory doc. Every choice produces a
        // different corpus of chunks, which changes every downstream result.
        Console.WriteLine("=== Advanced RAG Demo ===\n");
        Console.WriteLine("Chunking strategy (see 03-AdvancedRAG.md §4):");
        Console.WriteLine($"  1) Fixed-size ({rag.FixedChunkSize} chars, {rag.FixedChunkOverlap} overlap)  — cuts mid-sentence");
        Console.WriteLine($"  2) Sentence   ({rag.SentencesPerChunk} sentences per chunk)  — respects sentences");
        Console.WriteLine("  3) Paragraph  (split on blank lines)   — respects authorship");
        Console.Write("Selection (Enter for 3): ");
        var chunkChoice = Console.ReadLine()?.Trim();

        Func<string, IEnumerable<string>> chunker = chunkChoice switch
        {
            "1" => (text) => Chunkers.FixedSize(text, maxChars: rag.FixedChunkSize, overlap: rag.FixedChunkOverlap),
            "2" => (text) => Chunkers.BySentence(text, sentencesPerChunk: rag.SentencesPerChunk),
            _ => (text) => Chunkers.ByParagraph(text)
        };

        // STEP 2 — INGESTION.
        // Take each source document, chunk it, embed each chunk, and store.
        // This is what an offline "indexing" pipeline does in production.
        Console.WriteLine("\nIngesting sample corpus (chunking + embedding)...");
        var store = new InMemoryVectorStore();

        foreach (var srcDoc in SampleCorpus.Documents)
        {
            var chunks = chunker(srcDoc.Content).ToList();
            // Batch-embed all chunks of this doc in a single API call — cheaper
            // and faster than one call per chunk.
            var embeddings = await EmbeddingHelper.EmbedBatchAsync(embeddingClient, chunks);
            // Each chunk gets a stable id ("hr-vacation#chunk-1") that answers can cite,
            // plus the parent doc's metadata so it can be filtered later (§9).
            for (int i = 0; i < chunks.Count; i++)
            {
                store.Add(new Chunk(
                    Id: $"{srcDoc.Id}#chunk-{i}",
                    SourceDocId: srcDoc.Id,
                    Content: chunks[i],
                    Embedding: embeddings[i],
                    Category: srcDoc.Category,
                    Department: srcDoc.Department,
                    CreatedAt: srcDoc.CreatedAt));
            }
        }
        Console.WriteLine($"Indexed {store.Count} chunks from {SampleCorpus.Documents.Count} documents.\n");

        // STEP 3 — Interactive retrieval menu.
        while (true)
        {
            Console.WriteLine("\n--- Retrieval mode ---");
            Console.WriteLine("  1) Vector search only          (§5)");
            Console.WriteLine("  2) Keyword (BM25-lite) only    (§6)");
            Console.WriteLine("  3) Hybrid (Vector + BM25 + RRF)(§7)");
            Console.WriteLine("  4) Hybrid + Rerank             (§8)");
            Console.WriteLine("  5) Hybrid + Metadata filter    (§9)");
            Console.WriteLine("  6) Hybrid + Query rewriting    (§10)");
            Console.WriteLine("  7) FULL pipeline (all of above)(§11)");
            Console.WriteLine("  8) Ask a question with your chosen mode (chat)");
            Console.WriteLine("  0) Quit");
            Console.Write("Selection: ");
            var mode = Console.ReadLine()?.Trim();
            if (mode == "0") break;

            Console.Write("Query: ");
            var query = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(query)) continue;

            var retrieved = await RagRetriever.RetrieveAsync(mode, query, store, chatClient, embeddingClient);
            ResultPrinter.Print(retrieved);

            // Option 8 goes one step further — actually answer the question
            // using the retrieved chunks as LLM context (this is the "G" in RAG).
            if (mode == "8")
            {
                Console.WriteLine("\n--- Generating grounded answer ---");
                var answer = await AnswerGenerator.GenerateAsync(chatClient, query, retrieved);
                Console.WriteLine($"\nAnswer:\n{answer}\n");
            }
        }
    }
}
