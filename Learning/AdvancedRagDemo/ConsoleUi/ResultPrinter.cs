using AIMLAPP.Learning.AdvancedRagDemo.Models;

namespace AIMLAPP.Learning.AdvancedRagDemo.ConsoleUi;

// Console pretty-printer for retrieval results.
public static class ResultPrinter
{
    public static void Print(List<ScoredChunk> results, bool showScore = true)
    {
        if (results.Count == 0)
        {
            Console.WriteLine("(no results)");
            return;
        }

        Console.WriteLine();
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var scoreStr = showScore ? $"  [score={r.Score:F4}]" : "";
            Console.WriteLine($"#{i + 1}  {r.Chunk.Id}  dept={r.Chunk.Department}  cat={r.Chunk.Category}{scoreStr}");
            var preview = r.Chunk.Content.Replace("\n", " ").Trim();
            if (preview.Length > 180) preview = preview.Substring(0, 180) + "...";
            Console.WriteLine($"{preview}");
        }
    }
}
