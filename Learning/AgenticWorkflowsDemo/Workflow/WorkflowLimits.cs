using AIMLAPP.Configuration;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Workflow;

// Caps keep a confused model from looping until the API bill arrives.
// Same idea as MAX_TURNS in Chapter 1, applied to a graph instead of one chat loop.
// All four values are tunable in appsettings.json → AgenticWorkflows (see the table at
// the top of 04-AgenticWorkflows.md).
public static class WorkflowLimits
{
    // Reviewer score (1-10) that counts as a pass. Compared in code by BriefWorkflow.ApplyReview.
    public static int ReviewPassScore => AppSettings.Current.AgenticWorkflows.ReviewPassScore;
    // Rewrites allowed after a failed review. Bounds the draft → review loop.
    public static int MaxRevisions => AppSettings.Current.AgenticWorkflows.MaxRevisions;
    // Node cap for the code-routed graph (option 6). Backstop if routing ever cycles.
    public static int MaxNodeVisits => AppSettings.Current.AgenticWorkflows.MaxNodeVisits;
    // Hand-off cap for the model supervisor (option 5). Stops a supervisor that keeps
    // picking the same agent.
    public static int MaxAgentHops => AppSettings.Current.AgenticWorkflows.MaxAgentHops;
}
