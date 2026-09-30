using System.Text.RegularExpressions;

namespace AIMLAPP.Learning.ProductionAiDemo.Security;

// Strip identifiers before a prompt is logged or sent, and again on the way out.
// Logs, traces, and prompts sent to a third-party API all outlive the request,
// so personal data and keys must be removed first. See 07-ProductionAI.md §2.
// The patterns are deliberately simple shapes, not a complete PII detector.
public static class PiiRedactor
{
    private static readonly Regex Email = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex Ssn = new(
        @"\b\d{3}-\d{2}-\d{4}\b",
        RegexOptions.Compiled);

    // A key-shaped token. The demo uses a fake value, never a real secret.
    private static readonly Regex Secret = new(
        @"\bsk-[A-Za-z0-9-]{10,}",
        RegexOptions.Compiled);

    // Detect vs redact: guards use Contains* to block or skip caching;
    // Redact keeps the rest of the text usable for a log line or a prompt.
    public static bool ContainsEmail(string text) => Email.IsMatch(text);

    public static bool ContainsSsn(string text) => Ssn.IsMatch(text);

    public static bool ContainsSecret(string text) => Secret.IsMatch(text);

    public static string Redact(string text)
    {
        var withoutEmail = Email.Replace(text, "[email]");
        var withoutSsn = Ssn.Replace(withoutEmail, "[ssn]");
        return Secret.Replace(withoutSsn, "[secret]");
    }
}
