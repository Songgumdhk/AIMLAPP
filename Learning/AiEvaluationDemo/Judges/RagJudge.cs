using OpenAI.Chat;
using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Judges;

// POINTWISE LLM-AS-JUDGE — grades one (question, context, answer) against a written rubric.
// Three criteria, scored separately, because they fail for different reasons.
// See 05-AIEvaluation.md §5 (the criteria) and §7 (the judge).
public static class RagJudge
{
    // THE RUBRIC. Each criterion deliberately ignores something:
    //   - contextRelevance ignores the answer: good answer + wrong documents still fails.
    //   - answerRelevance ignores truth: a fluent lie can score high, "I don't know" low.
    //   - faithfulness ignores topic: is every claim backed by CONTEXT? Catches answers
    //     written from the model's memory instead of the documents.
    // One blended "quality" score would hide which of the three broke.
    //
    // WHY reason before score: the model writes its justification first, then commits to
    // a number that follows from it. A bare number is easy to emit and hard to audit.
    public const string SystemPrompt =
        "You grade a RAG answer. You do not answer the question yourself. " +
        "Score each criterion from 1 to 5 using only the text you are given. " +
        "contextRelevance: how well CONTEXT is about QUESTION. Ignore the answer. " +
        "answerRelevance: how well ANSWER addresses QUESTION. Ignore truth and ignore CONTEXT. " +
        "faithfulness: how well every factual claim in ANSWER is supported by CONTEXT. " +
        "Outside knowledge lowers faithfulness. 5 means fully supported. 1 means invented or contradicted. " +
        "Write each reason before you choose that score. " +
        "Reply as JSON: {\"contextRelevance\":{\"reason\":\"...\",\"score\":1},\"answerRelevance\":{\"reason\":\"...\",\"score\":1},\"faithfulness\":{\"reason\":\"...\",\"score\":1}}";

    public static async Task<RagJudgement> JudgeAsync(
        ChatClient client,
        string question,
        string context,
        string answer)
    {
        // CRITICAL: the judge never sees the gold document ids. Retrieval metrics already
        // used the answer key. The judge covers what you cannot hand-label every time,
        // and giving it the key would let it grade "matches the key" instead.
        var raw = await EvalChat.CompleteAsync(
            client,
            SystemPrompt,
            $"QUESTION:\n{question}\n\nCONTEXT:\n{context}\n\nANSWER:\n{answer}",
            json: true);

        using var document = EvalChat.Parse(raw);

        // Unreadable reply: all three marks become 0 ("unscored"), not a guess.
        // The harness keeps the 0 in the mean so the failure stays visible (§8).
        if (document is null)
        {
            var failed = new RubricMark(0, "Judge JSON could not be parsed.");
            return new RagJudgement(failed, failed, failed);
        }

        var root = document.RootElement;
        return new RagJudgement(
            EvalChat.ReadMark(root, "contextRelevance"),
            EvalChat.ReadMark(root, "answerRelevance"),
            EvalChat.ReadMark(root, "faithfulness"));
    }
}
