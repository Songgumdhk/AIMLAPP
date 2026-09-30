using System.Text.Json;

namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// Fake email sender. Gated by ApprovalPolicy.
// A side-effect tool: once sent, it can't be unsent, so a human must approve (§10).
public static class SendEmailTool
{
    public const string Name = "send_email";

    public static OpenAI.Chat.ChatTool Schema => OpenAI.Chat.ChatTool.CreateFunctionTool(
        functionName: Name,
        // The warning in the description makes the model less trigger-happy,
        // but it is not a safety control. ApprovalPolicy is.
        functionDescription:
            "Send an email on behalf of the user. This is a real-world action " +
            "that cannot be undone; use it only when the user explicitly asks " +
            "to send an email.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "to":      { "type": "string", "description": "Recipient email address." },
                "subject": { "type": "string", "description": "Email subject line." },
                "body":    { "type": "string", "description": "Email body text." }
            },
            "required": ["to", "body"]
        }
        """));

    public static Task<string> ExecuteAsync(JsonElement args)
    {
        var to = args.GetProperty("to").GetString();
        // subject is not in "required", so it may be missing.
        var subject = args.TryGetProperty("subject", out var s) ? s.GetString() : "(no subject)";
        var body = args.GetProperty("body").GetString();

        Console.WriteLine($"  [SIMULATED SEND] to={to} subject={subject} body={body}");
        return Task.FromResult($"Email successfully sent to {to}.");
    }
}
