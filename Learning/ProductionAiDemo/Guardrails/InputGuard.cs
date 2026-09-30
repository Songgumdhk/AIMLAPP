using AIMLAPP.Learning.ProductionAiDemo.Security;

namespace AIMLAPP.Learning.ProductionAiDemo.Guardrails;

// Runs before the cache lookup and before the model.
// A blocked question never becomes a tool call or a billable request.
// Each rule has a name, so a block can be logged and explained. See 07-ProductionAI.md §3.
public static class InputGuard
{
    // WHY a length cap: long input means more input tokens on the bill,
    // and more room to hide an injection inside a wall of text.
    public const int MaxChars = 500;

    public static GuardDecision Check(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new GuardDecision(false, "empty", "The question is empty.");

        if (text.Length > MaxChars)
            return new GuardDecision(false, "length", $"Over {MaxChars} characters.");

        if (InjectionGuard.Hits(text))
            return new GuardDecision(false, "injection", "Instruction-override phrase in the user text.");

        // A key pasted into a question would travel to the model API and into any log.
        if (PiiRedactor.ContainsSecret(text))
            return new GuardDecision(false, "secret", "Key-shaped text. Do not send it to the model.");

        return new GuardDecision(true, "ok", "");
    }
}
