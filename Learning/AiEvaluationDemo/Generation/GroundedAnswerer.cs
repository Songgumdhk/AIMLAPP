using System.Text;
using OpenAI.Chat;
using AIMLAPP.Learning.AiEvaluationDemo.Judges;
using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Generation;

// THE SYSTEM UNDER TEST — writes an answer from the retrieved documents.
// The judges grade what this produces. The prompt is one of the things you are allowed
// to change between eval runs (§2); rerun option 5 after you do.
public static class GroundedAnswerer
{
    // WHY an exact "I don't know" sentence: refusing when the context lacks the fact is a
    // product decision, and a fixed phrase makes it easy to spot. Evaluation is how you
    // find out whether the model actually follows it (§5). A refusal can score low on
    // answer relevance and still be fully faithful.
    public const string Strict =
        "Answer the question using only CONTEXT. " +
        "If CONTEXT does not contain the answer, reply exactly: I don't know based on the provided documents. " +
        "Do not add facts from outside CONTEXT. Write at most 4 sentences.";

    // Negative control. A shipped prompt should look like Strict.
    // This one exists so the claim checker has invented text to catch, the way a unit
    // test needs a failing input to prove it can fail (§6).
    public const string Ungrounded =
        "Answer with specific company-policy facts. " +
        "If CONTEXT does not contain the fact, invent a concrete detail and state it as policy. " +
        "Do not say you are unsure. Write at most 4 sentences.";

    public static async Task<string> AnswerAsync(
        ChatClient client,
        string instructions,
        string question,
        IReadOnlyList<RetrievedHit> hits)
    {
        var context = FormatContext(hits);
        return await EvalChat.CompleteAsync(
            client,
            instructions,
            $"CONTEXT:\n{context}\n\nQUESTION: {question}",
            json: false);
    }

    // CRITICAL: the judges must see the same context text the answerer saw. Callers pass
    // this exact string to RagJudge and HallucinationChecker, or faithfulness would be
    // graded against different evidence than the answer was written from.
    public static string FormatContext(IReadOnlyList<RetrievedHit> hits)
    {
        var sb = new StringBuilder();
        foreach (var hit in hits)
        {
            sb.Append('[').Append(hit.DocId).Append("] ").AppendLine(hit.Title);
            sb.AppendLine(hit.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
