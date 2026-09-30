// =============================================================================
//  Chapter 5 — AI Evaluation Demo (entry point)
// =============================================================================
//
//  PURPOSE
//  -------
//  Show the four pieces of Chapter 5 (see the README roadmap) as numbers you can re-run:
//  RAG evaluation, retrieval evaluation, hallucination detection, LLM-as-judge.
//
//  Folder map (matches 05-AIEvaluation.md):
//    Models/        scores, claims, the retrieval report
//    Data/          LabeledSet, JudgeFixtures
//    Retrieval/     CorpusIndex (naive, keyword, vector)
//    Metrics/       RetrievalMetrics, RetrievalHarness, FixtureCheck
//    Generation/    GroundedAnswerer
//    Judges/        RagJudge, HallucinationChecker, PairwiseJudge
//    ConsoleUi/     EvalPrinter
//
//  HOW TO RUN
//  ----------
//  From Program.cs, menu option 9.
// =============================================================================

using OpenAI.Chat;
using OpenAI.Embeddings;
using System.Text;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.AiEvaluationDemo.ConsoleUi;
using AIMLAPP.Learning.AiEvaluationDemo.Data;
using AIMLAPP.Learning.AiEvaluationDemo.Generation;
using AIMLAPP.Learning.AiEvaluationDemo.Judges;
using AIMLAPP.Learning.AiEvaluationDemo.Metrics;
using AIMLAPP.Learning.AiEvaluationDemo.Models;
using AIMLAPP.Learning.AiEvaluationDemo.Retrieval;

namespace AIMLAPP.Learning.AiEvaluationDemo;

public static class AiEvaluationDemo
{
    public static async Task RunAsync(string apiKey)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // STEP 0: Verify the frozen set still matches the chapter, before any API spend.
        FixtureCheck.Verify();

        // One chat model both answers and judges (OpenAI:ChatModel); OpenAI:EmbeddingModel
        // powers the vector retriever. Scores are only comparable between runs that used
        // the same models, so note these settings next to any result you keep.
        var chat = new ChatClient(AppSettings.Current.OpenAI.ChatModel, apiKey);
        var embeddings = new EmbeddingClient(AppSettings.Current.OpenAI.EmbeddingModel, apiKey);
        CorpusIndex? index = null;

        Console.WriteLine("=== AI Evaluation Demo ===");
        Console.WriteLine("Read 05-AIEvaluation.md alongside the menu.");
        Console.WriteLine($"{LabeledSet.Documents.Count} documents, {LabeledSet.Cases.Count} labeled questions, k = {RetrievalMetrics.K}.");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- Pick a piece ---");
            Console.WriteLine("  1) Retrieval evaluation      — precision, recall, hit, MRR");
            Console.WriteLine("  2) RAG evaluation            — retrieve, answer, then score the path");
            Console.WriteLine("  3) Hallucination detection   — which claims does the context support?");
            Console.WriteLine("  4) LLM-as-judge              — rubric scores, and an order swap");
            Console.WriteLine("  5) Full labeled run          — retrieval on every question, then optional grading");
            Console.WriteLine("  0) Quit");
            Console.Write("Selection: ");
            var mode = Console.ReadLine()?.Trim();

            await (mode switch
            {
                "0" => Task.CompletedTask,
                "1" => RunRetrievalAsync(embeddings, await LoadIndexAsync()),
                "2" => RunRagAsync(chat, embeddings, await LoadIndexAsync()),
                "3" => RunHallucinationAsync(chat, embeddings, () => LoadIndexAsync()),
                "4" => RunJudgeAsync(chat),
                "5" => RunFullAsync(chat, embeddings, await LoadIndexAsync()),
                _ => InvalidModeAsync()
            });
        }

        static Task InvalidModeAsync()
        {
            Console.WriteLine("Pick a number from the menu.");
            return Task.CompletedTask;
        }

        // Built lazily and reused: the corpus is frozen, so its embeddings never change
        // during a session. Option 4 uses fixed text and never pays for embeddings.
        async Task<CorpusIndex> LoadIndexAsync()
        {
            if (index is not null) return index;
            Console.WriteLine();
            Console.WriteLine($"Indexing {LabeledSet.Documents.Count} documents (one embedding request)...");
            index = await CorpusIndex.BuildAsync(embeddings, LabeledSet.Documents);
            Console.WriteLine("Indexed.");
            return index;
        }
    }

    private static async Task RunRetrievalAsync(EmbeddingClient embeddings, CorpusIndex index)
    {
        Console.WriteLine();
        Console.WriteLine("Embedding the 6 labeled questions, then scoring three retrievers.");
        var report = await RetrievalHarness.RunAsync(index, embeddings);
        EvalPrinter.PrintRetrieval(report);
    }

    private static async Task RunRagAsync(ChatClient chat, EmbeddingClient embeddings, CorpusIndex index)
    {
        Console.WriteLine();
        Console.WriteLine("Labeled questions:");
        for (var i = 0; i < LabeledSet.Cases.Count; i++)
            Console.WriteLine($"  {i + 1}) {LabeledSet.Cases[i].Question}");
        Console.WriteLine("Type a number, or type your own question.");
        Console.Write("Question: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) return;

        var evalCase = MatchCase(input.Trim());
        var question = evalCase?.Question ?? input.Trim();

        Console.WriteLine();
        Console.WriteLine("Retrieving, writing a strict answer, then calling the rubric judge and the claim checker.");
        // STEP 1: Retrieve the vector top K.
        var hits = await RetrieveVectorAsync(embeddings, index, question);

        // STEP 2: Retrieval metrics only when the question has gold ids. A typed question
        // gets judge scores but no precision or recall. Never let a judge invent gold ids.
        var retrieval = evalCase is null
            ? null
            : RetrievalMetrics.Score(hits.Select(h => h.DocId).ToList(), evalCase.RelevantDocIds);

        // STEP 3: Three model calls, one job each: answer, rubric, then claim check.
        // The claim list is the faithfulness score opened up (§5-6).
        var answer = await GroundedAnswerer.AnswerAsync(chat, GroundedAnswerer.Strict, question, hits);
        var context = GroundedAnswerer.FormatContext(hits);
        var judgement = await RagJudge.JudgeAsync(chat, question, context, answer);
        var claims = await HallucinationChecker.CheckAsync(chat, context, answer);
        EvalPrinter.PrintRagPath(evalCase, question, hits, retrieval, answer, judgement, claims);
    }

    private static async Task RunHallucinationAsync(
        ChatClient chat,
        EmbeddingClient embeddings,
        Func<Task<CorpusIndex>> loadIndex)
    {
        Console.WriteLine();
        Console.WriteLine("  1) Planted answers        — one faithful, one with invented facts");
        Console.WriteLine("  2) Strict vs ungrounded   — same missing fact, two prompts");
        Console.WriteLine("  0) Back");
        Console.Write("Selection: ");
        var choice = Console.ReadLine()?.Trim();

        // WHY fixed answers and a fixed context: no retrieval and no generation, so the only
        // moving part is the checker. A bad search cannot confuse the lesson.
        if (choice == "1")
        {
            Console.WriteLine();
            Console.WriteLine("Context is the vacation policy only. Both answers are fixed text, not a fresh generation.");
            await PrintPlantedAsync(chat, "Faithful answer", JudgeFixtures.FaithfulVacation);
            await PrintPlantedAsync(chat, "Invented answer", JudgeFixtures.InventedVacation);
            Console.WriteLine();
            Console.WriteLine("The first sentence of the invented answer is in the policy. The cash payout and the contractor benefit are not.");
            Console.WriteLine("The rate counts claims, so a mostly-true answer can still be partly hallucinated.");
            return;
        }

        if (choice == "0") return;

        // Same question, same retrieved context, two prompts. Only the instruction changes,
        // so any difference in the claim labels comes from the prompt.
        if (choice == "2")
        {
            var index = await loadIndex();
            var question = JudgeFixtures.MissingFactQuestion;
            Console.WriteLine();
            Console.WriteLine(question);
            Console.WriteLine("That fact is not in the corpus. The ungrounded prompt is a negative control, not a prompt to ship.");
            var hits = await RetrieveVectorAsync(embeddings, index, question);
            Console.WriteLine();
            Console.WriteLine("Strict prompt:");
            var strict = await GroundedAnswerer.AnswerAsync(chat, GroundedAnswerer.Strict, question, hits);
            Console.WriteLine(strict.Trim());
            EvalPrinter.PrintClaims(await HallucinationChecker.CheckAsync(chat, GroundedAnswerer.FormatContext(hits), strict));

            Console.WriteLine();
            Console.WriteLine("Ungrounded prompt:");
            var loose = await GroundedAnswerer.AnswerAsync(chat, GroundedAnswerer.Ungrounded, question, hits);
            Console.WriteLine(loose.Trim());
            EvalPrinter.PrintClaims(await HallucinationChecker.CheckAsync(chat, GroundedAnswerer.FormatContext(hits), loose));
            return;
        }

        Console.WriteLine("Pick a number from the menu.");
    }

    private static async Task PrintPlantedAsync(ChatClient chat, string title, string answer)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(answer);
        var claims = await HallucinationChecker.CheckAsync(chat, JudgeFixtures.VacationContext, answer);
        EvalPrinter.PrintClaims(claims);
    }

    private static async Task RunJudgeAsync(ChatClient chat)
    {
        Console.WriteLine();
        Console.WriteLine("  1) Pointwise rubric       — a true answer and a fluent lie");
        Console.WriteLine("  2) Pairwise order swap    — two faithful answers, judged twice");
        Console.WriteLine("  0) Back");
        Console.Write("Selection: ");
        var choice = Console.ReadLine()?.Trim();

        // Pointwise: the fluent lie is on topic, so answer relevance can stay high while
        // faithfulness should drop. That split is why the rubric has separate scores (§7).
        if (choice == "1")
        {
            var context = JudgeFixtures.ExpenseContext;
            var question = JudgeFixtures.ExpenseQuestion;
            Console.WriteLine();
            Console.WriteLine("Context: " + context);
            Console.WriteLine();
            Console.WriteLine("Calling the judge twice. Same question, same context, different answers.");
            var faithful = await RagJudge.JudgeAsync(chat, question, context, JudgeFixtures.FaithfulExpense);
            var lie = await RagJudge.JudgeAsync(chat, question, context, JudgeFixtures.FluentLie);
            EvalPrinter.PrintRubricPair("True answer: " + JudgeFixtures.FaithfulExpense, faithful);
            EvalPrinter.PrintRubricPair("Fluent lie: " + JudgeFixtures.FluentLie, lie);
            Console.WriteLine();
            Console.WriteLine("Read faithfulness against answer relevance on the fluent lie.");
            Console.WriteLine("If faithfulness did not drop, the judge missed an invented policy. Judges have error too.");
            return;
        }

        if (choice == "0") return;

        if (choice == "2")
        {
            await RunOrderSwapAsync(chat);
            return;
        }

        Console.WriteLine("Pick a number from the menu.");
    }

    // POSITION-BIAS TEST — judge the same two answers twice with A and B swapped.
    // A consistent judge flips its letter and keeps the same text. A single pairwise call
    // is not a stable measurement until the swap agrees (§7).
    private static async Task RunOrderSwapAsync(ChatClient chat)
    {
        var context = JudgeFixtures.ExpenseContext;
        var question = JudgeFixtures.ExpenseQuestion;
        var brief = JudgeFixtures.BriefExpense;
        var detailed = JudgeFixtures.DetailedExpense;

        Console.WriteLine();
        Console.WriteLine("Both answers are supported by the expense policy. The only change on pass 2 is which one is labeled A.");
        Console.WriteLine();
        Console.WriteLine("Pass 1 — A is Brief, B is Detailed");
        var first = await PairwiseJudge.JudgeAsync(chat, question, context, brief, detailed);
        PrintPass(first);

        Console.WriteLine();
        Console.WriteLine("Pass 2 — A is Detailed, B is Brief");
        var second = await PairwiseJudge.JudgeAsync(chat, question, context, detailed, brief);
        PrintPass(second);

        // Translate each letter back to the text it labeled, then compare texts, not letters.
        var firstPick = PairwiseJudge.ChosenId(first, "Brief", "Detailed");
        var secondPick = PairwiseJudge.ChosenId(second, "Detailed", "Brief");
        Console.WriteLine();
        if (string.IsNullOrEmpty(first.Winner) || string.IsNullOrEmpty(second.Winner))
        {
            Console.WriteLine("One of the judge replies did not pick A, B, or tie.");
        }
        else if (first.Winner == "tie" && second.Winner == "tie")
        {
            Console.WriteLine("Both passes tied. Order did not create a winner.");
        }
        else if (firstPick is not null && firstPick == secondPick)
        {
            Console.WriteLine($"Both passes picked {firstPick}. The preference survived the swap.");
        }
        else
        {
            Console.WriteLine("The preferred answer changed when the order changed. That is position bias.");
            Console.WriteLine("A single pairwise call is not a stable measurement until you check the swap.");
        }
    }

    private static void PrintPass(PairwiseResult result)
    {
        var winner = string.IsNullOrEmpty(result.Winner) ? "unparsed" : result.Winner;
        Console.WriteLine("Winner: " + winner);
        if (!string.IsNullOrWhiteSpace(result.Reason))
            Console.WriteLine(result.Reason.Trim());
    }

    private static async Task RunFullAsync(ChatClient chat, EmbeddingClient embeddings, CorpusIndex index)
    {
        Console.WriteLine();
        Console.WriteLine("Scoring every labeled question with naive, keyword, and vector retrieval.");
        var report = await RetrievalHarness.RunAsync(index, embeddings);
        EvalPrinter.PrintRetrieval(report);

        Console.WriteLine();
        Console.WriteLine($"Grade all {LabeledSet.Cases.Count} answers? About two model calls per question (strict answer + rubric).");
        Console.WriteLine("Claim-level checks stay in option 3.");
        Console.Write("Grade answers? [y/n]: ");
        var yes = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (yes is not ("y" or "yes")) return;

        // Grade the system you would ship: the strict prompt on the vector top K. The claim
        // checker is left out to keep this pass to about two calls per question (§8).
        var judgements = new List<RagJudgement>();
        for (var i = 0; i < LabeledSet.Cases.Count; i++)
        {
            var evalCase = LabeledSet.Cases[i];
            Console.WriteLine($"[{i + 1}/{LabeledSet.Cases.Count}] {evalCase.Id}");

            // Reuse the exact hits the retrieval table scored, so Hit and Faith describe
            // the same documents. Hit 0 + Faith 5 and Hit 1 + Faith 2 are different bugs.
            var hits = report.VectorHits[i];
            var answer = await GroundedAnswerer.AnswerAsync(chat, GroundedAnswerer.Strict, evalCase.Question, hits);
            var judgement = await RagJudge.JudgeAsync(
                chat,
                evalCase.Question,
                GroundedAnswerer.FormatContext(hits),
                answer);
            judgements.Add(judgement);
            Console.WriteLine("  " + Trim(answer));
        }

        EvalPrinter.PrintGradeTable(report.Vector, judgements);
    }

    private static async Task<List<RetrievedHit>> RetrieveVectorAsync(
        EmbeddingClient embeddings,
        CorpusIndex index,
        string question)
    {
        var vectors = await CorpusIndex.EmbedQueriesAsync(embeddings, [question]);
        return index.VectorSearch(vectors[0], RetrievalMetrics.K);
    }

    private static EvalCase? MatchCase(string input)
    {
        if (int.TryParse(input, out var n) && n >= 1 && n <= LabeledSet.Cases.Count)
            return LabeledSet.Cases[n - 1];

        return LabeledSet.Cases.FirstOrDefault(c =>
            c.Question.Equals(input, StringComparison.OrdinalIgnoreCase));
    }

    private static string Trim(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= 140 ? flat : flat[..140] + "...";
    }
}
