using System.Text.RegularExpressions;
using AIMLAPP.Learning.ProductionAiDemo.Security;

namespace AIMLAPP.Learning.ProductionAiDemo.Guardrails;

// Runs after the model and before the answer is cached or returned.
// A refusal is allowed. A factual answer must cite a document id like [hr-vacation].
// The model's text is untrusted output, just like user input. See 07-ProductionAI.md §3.
public static class OutputGuard
{
    // WHY require a citation: an answer tied to a [doc-id] can be checked against
    // the source. An uncited claim may be made up, so it is blocked.
    private static readonly Regex Citation = new(@"\[[a-z0-9-]+\]", RegexOptions.Compiled);

    public static GuardDecision Check(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return new GuardDecision(false, "empty", "The model returned no text.");

        // PII checked first: even a well-cited answer must not leak personal data or a key.
        if (PiiRedactor.ContainsEmail(answer) || PiiRedactor.ContainsSsn(answer) || PiiRedactor.ContainsSecret(answer))
            return new GuardDecision(false, "pii", "The answer contains an email, an SSN shape, or a key shape.");

        // "I don't know" is the safe answer we asked for. Blocking it would push
        // the model toward guessing.
        if (answer.Contains("I don't know", StringComparison.OrdinalIgnoreCase))
            return new GuardDecision(true, "refusal", "A refusal does not need a citation.");

        if (!Citation.IsMatch(answer))
            return new GuardDecision(false, "citation", "A factual answer must cite a document id in brackets.");

        return new GuardDecision(true, "ok", "");
    }
}
