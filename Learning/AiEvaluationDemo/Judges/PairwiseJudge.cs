using OpenAI.Chat;
using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Judges;

// PAIRWISE LLM-AS-JUDGE — "which of these two answers is better?" instead of a 1-5 score.
// Comparisons are often easier for a model than absolute scores, but they bring a new
// error: position bias (the label A or B, not the text, moves the pick).
// The demo calls this twice with A and B swapped. See 05-AIEvaluation.md §7.
public static class PairwiseJudge
{
    // WHY "ignore style, length, and politeness": judges tend to prefer longer, more
    // polished answers. The demo's two answers are both faithful and differ mainly in
    // length, so the rubric gives no real reason to prefer either one.
    public const string SystemPrompt =
        "You compare two answers for faithfulness to CONTEXT. Ignore style, length, and politeness. " +
        "Pick the answer whose factual claims are better supported by CONTEXT. " +
        "If they are equally supported, winner is tie. " +
        "Reply as JSON: {\"reason\":\"...\",\"winner\":\"A\"}. " +
        "winner is A, B, or tie.";

    public static async Task<PairwiseResult> JudgeAsync(
        ChatClient client,
        string question,
        string context,
        string answerA,
        string answerB)
    {
        var raw = await EvalChat.CompleteAsync(
            client,
            SystemPrompt,
            $"QUESTION:\n{question}\n\nCONTEXT:\n{context}\n\nAnswer A:\n{answerA}\n\nAnswer B:\n{answerB}",
            json: true);

        using var document = EvalChat.Parse(raw);
        if (document is null)
            return new PairwiseResult("Judge JSON could not be parsed.", "");

        var root = document.RootElement;
        return new PairwiseResult(
            EvalChat.ReadString(root, "reason"),
            Normalize(EvalChat.ReadString(root, "winner")));
    }

    // Maps the letter back to the underlying text ("Brief" or "Detailed").
    // CRITICAL: compare passes by text, not by letter. After the swap, a consistent judge
    // flips its letter (A -> B) and keeps the same text. Same letter twice = position bias.
    public static string? ChosenId(PairwiseResult result, string idA, string idB) =>
        result.Winner switch
        {
            "A" => idA,
            "B" => idB,
            _ => null,
        };

    // Accept common spellings of the pick. "" means unparsed, which the demo reports
    // instead of treating as a tie.
    private static string Normalize(string winner)
    {
        var value = winner.Trim().ToLowerInvariant();
        return value switch
        {
            "a" or "answer a" or "first" => "A",
            "b" or "answer b" or "second" => "B",
            "tie" or "equal" or "draw" => "tie",
            _ => "",
        };
    }
}
