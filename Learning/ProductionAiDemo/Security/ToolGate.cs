namespace AIMLAPP.Learning.ProductionAiDemo.Security;

// The model may ask for a tool. Your code decides if this role may run it.
// Unknown tool names are denied. A missing allow-list entry must not default to yes.
// Least privilege: each role gets only the tools it needs. See 07-ProductionAI.md §2.
public static class ToolGate
{
    // WHY check the caller's role, not the model's request: an injected prompt can
    // make the model ask for any tool. Permission must come from who the user is.
    public static bool Allowed(string role, string tool) => (role, tool) switch
    {
        ("employee" or "admin", "lookup_policy") => true,
        // Side-effect tools stay off the employee role.
        // Chapter 1 still requires a human before send or delete, even for admin.
        ("admin", "export_customers" or "delete_record") => true,
        // CRITICAL: the default is deny. Unknown roles and tools (drop_database) land here.
        _ => false,
    };
}
