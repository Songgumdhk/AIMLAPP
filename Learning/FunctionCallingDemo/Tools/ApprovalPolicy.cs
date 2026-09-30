namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// HUMAN-IN-THE-LOOP POLICY — decides which tools pause for a human "yes" (§10).
// Anything with real-world side effects goes through the HITL gate.
// Exercise #4 adds delete_record to this list.
// WHY in code, not in the prompt: a description like "confirm first" is only a
// suggestion the model can ignore. This check is enforced every time.
public static class ApprovalPolicy
{
    public static bool RequiresHumanApproval(string toolName) => toolName switch
    {
        SendEmailTool.Name => true,
        DeleteRecordTool.Name => true,
        _ => false
    };
}
