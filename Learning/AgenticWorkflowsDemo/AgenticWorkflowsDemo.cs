// =============================================================================
//  Chapter 4 — Agentic Workflows Demo (entry point)
// =============================================================================
//
//  PURPOSE
//  -------
//  Show the five pieces of Chapter 4 (see the README roadmap) as code you can run:
//  Planning, State, Memory, Human-in-the-loop, Multi-agent.
//
//  Folder map (matches 04-AgenticWorkflows.md):
//    Models/        WorkflowState, PlanStep
//    Knowledge/     PolicyCorpus          (the tool the researcher calls)
//    Memory/        MemoryStore           (long-term facts on disk)
//    Persistence/   RunStore              (checkpoint the whole run)
//    Agents/        Planner, Researcher, Writer, Reviewer, Supervisor
//    Workflow/      TicketStateMachine, BriefWorkflow, HumanGate
//    ConsoleUi/     WorkflowPrinter
//
//  HOW TO RUN
//  ----------
//  From Program.cs, menu option 8.
//  Every agent uses the model in appsettings.json → OpenAI:ChatModel.
//  Loop caps and the pass score are in appsettings.json → AgenticWorkflows.
// =============================================================================

using OpenAI.Chat;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;
using AIMLAPP.Learning.AgenticWorkflowsDemo.ConsoleUi;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Memory;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Persistence;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Workflow;
using AIMLAPP.Configuration;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo;

// The menu, plus the two engine loops that drive the graphs:
// the supervisor loop (option 5, model-routed) and DriveUntilPauseAsync (options 4 and 6,
// code-routed). Compare them side by side to see §8's trade-off.
public static class AgenticWorkflowsDemo
{
    public static async Task RunAsync(string apiKey)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var client = new ChatClient(AppSettings.Current.OpenAI.ChatModel, apiKey);
        var memory = new MemoryStore();
        var runs = new RunStore();

        Console.WriteLine("=== Agentic Workflows Demo ===");
        Console.WriteLine("Read 04-AgenticWorkflows.md alongside the menu.\n");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- Pick a piece ---");
            Console.WriteLine("  1) Planning                         — the model writes steps, then stops");
            Console.WriteLine("  2) State machine                    — branches and a loop, no model");
            Console.WriteLine("  3) Memory                           — a fact on disk vs notes that die with the run");
            Console.WriteLine("  4) Human-in-the-loop                — pause, approve / edit / reject, resume");
            Console.WriteLine("  5) Multi-agent                      — a supervisor picks the next specialist");
            Console.WriteLine("  6) Full workflow                    — plan, route, review loop, human, checkpoint");
            Console.WriteLine("  0) Quit");
            Console.Write("Selection: ");
            var mode = Console.ReadLine()?.Trim();

            switch (mode)
            {
                case "0": return;
                case "1": await RunPlanningAsync(client); break;
                case "2": RunStateMachine(); break;
                case "3": await RunMemoryAsync(client, memory); break;
                case "4": await RunHitlAsync(client, runs); break;
                case "5": await RunMultiAgentAsync(client, memory); break;
                case "6": await RunFullAsync(client, memory, runs); break;
                default: Console.WriteLine("Pick a number from the menu."); break;
            }
        }
    }

    private static async Task RunPlanningAsync(ChatClient client)
    {
        Console.WriteLine();
        Console.WriteLine("The planner returns a plan. Nothing is executed.");
        Console.WriteLine("Try: Write an onboarding brief for a new engineer covering vacation and security.");
        Console.Write("Goal: ");
        var goal = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(goal)) return;

        var plan = await PlannerAgent.CreatePlanAsync(client, goal);
        Console.WriteLine();
        for (var i = 0; i < plan.Count; i++)
            Console.WriteLine($"  {i + 1}. [{plan[i].Action}] {plan[i].Instruction}");
        Console.WriteLine();
        Console.WriteLine("Mode 6 is what runs this plan. The action names are the graph nodes.");
    }

    private static void RunStateMachine()
    {
        Console.WriteLine();
        Console.WriteLine("A ticket walks a fixed graph. Watch the state columns change, then the arrow.");
        Console.Write("Category (billing / technical / general / other): ");
        var category = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(category)) category = "billing";

        Console.Write("Fail the first resolve attempt? [y/n] (Enter = y): ");
        var fail = Console.ReadLine()?.Trim().ToLowerInvariant();
        var failFirst = fail is not ("n" or "no");

        Console.WriteLine();
        TicketStateMachine.Run(category, failFirst);
        Console.WriteLine();
        Console.WriteLine("Each row is one node. The arrow is the edge it chose.");
        Console.WriteLine("'other' skips the desks and goes to escalate. 'y' on the first attempt loops back to classify.");
    }

    private static async Task RunMemoryAsync(ChatClient client, MemoryStore memory)
    {
        while (true)
        {
            var facts = memory.Load();
            Console.WriteLine();

            Console.WriteLine("Long-term facts on disk:");
            if (facts.Count == 0) Console.WriteLine("  (none)");
            foreach (var fact in facts) Console.WriteLine("  - " + fact);
            Console.WriteLine($"File: {memory.FilePath}");

            Console.WriteLine();
            Console.WriteLine("  1) Remember a fact");
            Console.WriteLine("  2) Write a welcome note (uses disk facts + a scratchpad note that is NOT saved)");
            Console.WriteLine("  3) Forget all facts");
            Console.WriteLine("  0) Back");
            Console.Write("Selection: ");
            var choice = Console.ReadLine()?.Trim();

            if (choice == "0") return;
            if (choice == "1")
            {
                Console.Write("Fact to remember: ");
                var fact = Console.ReadLine();
                memory.Remember(fact ?? "");
                Console.WriteLine("Saved. Quit the app and open this menu again — the fact is still there.");
            }
            else if (choice == "3")
            {
                memory.ForgetAll();
                Console.WriteLine("Long-term memory cleared.");
            }
            else if (choice == "2")
            {
                var state = new WorkflowState
                {
                    Goal = "Write a short welcome note for a new engineer.",
                    MemoryFacts = memory.Load(),
                };
                // This line is short-term. It is on the state for this call only.
                // WHY it never reaches facts.json: memory is chosen explicitly. Only option 1
                // (or an approval in option 6) calls Remember (§6).
                state.Scratchpad.Add("Mention that security training is due Friday of week one. Do not store this sentence as a user preference.");
                state.Plan.Add(new PlanStep
                {
                    Action = "draft",
                    Instruction = "Four lines or fewer. Follow any saved preferences."
                });
                Console.WriteLine("  … writer is drafting from memory + scratchpad");
                await WriterAgent.RunAsync(client, state);
                Console.WriteLine();
                Console.WriteLine(state.Draft);
                Console.WriteLine();
                Console.WriteLine("The Friday deadline was scratchpad-only. It is not in the facts file.");
                Console.WriteLine("A preference you saved in option 1 is in the facts file, so the next run still sees it.");
            }
        }
    }

    private static async Task RunHitlAsync(ChatClient client, RunStore runs)
    {
        Console.WriteLine();
        Console.WriteLine("This drafts an email and then STOPS. Approving is a second step.");
        // A one-step plan that starts at "draft": same engine as option 6, minus the planner
        // and researcher, so the human gate is the only thing to watch.
        var state = new WorkflowState
        {
            Goal = "Draft a short email to HR asking them to confirm vacation carry-over. Do not invent a number of days.",
            CurrentNode = "draft",
        };
        state.Plan.Add(new PlanStep
        {
            Action = "draft",
            Instruction = state.Goal
        });

        await DriveUntilPauseAsync(client, state, runs);
        await ResolveHumanAsync(client, state, runs, memory: null);
    }

    private static async Task RunMultiAgentAsync(ChatClient client, MemoryStore memory)
    {
        Console.WriteLine();
        Console.WriteLine("No plan file. A supervisor model chooses researcher, writer, reviewer, or done.");
        Console.WriteLine("Try: Write an onboarding brief for a new engineer covering vacation and security.");
        Console.Write("Goal: ");
        var goal = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(goal)) return;

        var state = new WorkflowState
        {
            Goal = goal.Trim(),
            MemoryFacts = memory.Load(),
            CurrentNode = "supervisor",
        };

        // SUPERVISOR LOOP (model-routed). Every hop costs one extra model call just to choose
        // the next agent. CRITICAL: the hop cap is the only thing that stops a supervisor that
        // keeps picking the same specialist. Cap comes from appsettings.json →
        // AgenticWorkflows:MaxAgentHops.
        for (var hop = 1; hop <= WorkflowLimits.MaxAgentHops && state.Status == "running"; hop++)
        {
            Console.WriteLine($"  … supervisor hop {hop}");
            // STEP 1: The model picks the next specialist and gives a reason for the trace.
            var decision = await SupervisorAgent.DecideAsync(client, state);
            var before = state.Trace.Count;
            state.Trace.Add($"[supervisor] → {decision.Next} ({decision.Reason})");

            // STEP 2: Code dispatches to the chosen specialist. Specialists never call each
            // other; each one reads and writes the shared state, then control returns here.
            switch (decision.Next)
            {
                case "researcher":
                    Console.WriteLine("  … researcher");
                    await ResearcherAgent.RunAsync(client, state);
                    break;
                case "writer":
                    Console.WriteLine("  … writer");
                    await WriterAgent.RunAsync(client, state);
                    break;
                case "reviewer":
                    Console.WriteLine("  … reviewer");
                    await ReviewerAgent.RunAsync(client, state);
                    // Code still counts revisions, so the supervisor sees how many rewrites
                    // are left. The model routes; the bookkeeping stays deterministic.
                    if (state.ReviewScore > 0 && state.ReviewScore < WorkflowLimits.ReviewPassScore
                        && state.RevisionCount < WorkflowLimits.MaxRevisions)
                    {
                        state.RevisionCount++;
                    }
                    break;
                default:
                    state.Status = "completed";
                    break;
            }

            WorkflowPrinter.PrintNew(state, before);
        }

        // Hitting the cap is logged as its own status, so a stuck run is visible, not silent.
        if (state.Status == "running")
        {
            state.Status = "stopped";
            state.Trace.Add($"[!!] hop cap ({WorkflowLimits.MaxAgentHops}) reached");
        }

        WorkflowPrinter.PrintFinal(state, savedPath: null);
    }

    private static async Task RunFullAsync(ChatClient client, MemoryStore memory, RunStore runs)
    {
        Console.WriteLine();
        Console.WriteLine("  1) New goal");
        Console.WriteLine("  2) Resume a saved run");
        Console.Write("Selection (Enter = 1): ");
        var choice = Console.ReadLine()?.Trim();

        WorkflowState? state;
        if (choice == "2")
        {
            // RESUME: the checkpoint already holds CurrentNode and Status, so the same code
            // below continues the loop or reopens the approval prompt (§9).
            state = PickRun(runs);
            if (state is null) return;
            Console.WriteLine($"Resuming {state.RunId} from node '{state.CurrentNode}' ({state.Status}).");
        }
        else
        {
            Console.WriteLine("Try: Write an onboarding brief for a new engineer covering vacation and security.");
            Console.Write("Goal: ");
            var goal = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(goal)) return;
            // Long-term facts are copied onto the state once, at the start, so the checkpoint
            // records exactly what the writer was allowed to see.
            state = new WorkflowState
            {
                Goal = goal.Trim(),
                MemoryFacts = memory.Load(),
                CurrentNode = "plan",
            };
        }

        if (state.Status == "running")
            await DriveUntilPauseAsync(client, state, runs);

        if (state.Status == "waiting_for_human")
            await ResolveHumanAsync(client, state, runs, memory);
        else
            WorkflowPrinter.PrintFinal(state, Path.Combine(runs.DirectoryPath, state.RunId + ".json"));
    }

    // CODE-ROUTED ENGINE LOOP: run one node, checkpoint, repeat until the graph pauses,
    // finishes, or hits the node cap (appsettings.json → AgenticWorkflows:MaxNodeVisits).
    private static async Task DriveUntilPauseAsync(ChatClient client, WorkflowState state, RunStore runs)
    {
        var visits = 0;
        while (state.Status == "running" && visits++ < WorkflowLimits.MaxNodeVisits)
        {
            var before = state.Trace.Count;
            await BriefWorkflow.StepAsync(client, state);
            // CRITICAL: save after EVERY node. The "hitl" node's checkpoint is written here,
            // before the approval prompt, so quitting at the prompt loses nothing (§7, §9).
            runs.Save(state);
            WorkflowPrinter.PrintNew(state, before);
        }

        if (state.Status == "running")
        {
            state.Status = "stopped";
            state.Trace.Add($"[!!] node cap ({WorkflowLimits.MaxNodeVisits}) reached");
            runs.Save(state);
        }
        else if (state.Status == "waiting_for_human")
        {
            Console.WriteLine($"  checkpoint: {Path.Combine(runs.DirectoryPath, state.RunId + ".json")}");
            Console.WriteLine("  Stop the app here if you want Exercise 6: resume this run id from option 6.");
        }
    }

    private static async Task ResolveHumanAsync(
        ChatClient client,
        WorkflowState state,
        RunStore runs,
        MemoryStore? memory)
    {
        while (state.Status == "waiting_for_human")
        {
            var decision = HumanGate.Ask(state);
            if (decision == "approve")
            {
                // The side effect ("sent" / "published") only happens after a person approves.
                state.Status = "completed";
                state.Trace.Add("[hitl] approved");
                // EPISODIC MEMORY: one short line recording that it happened, not the draft.
                // Saving whole drafts would bloat every future prompt (§6).
                memory?.Remember("history: published a brief for: " + TrimGoal(state.Goal));
            }
            else if (decision == "reject")
            {
                state.Status = "rejected";
                state.Trace.Add("[hitl] rejected");
            }
            else
            {
                // EDIT: reopen draft + review, run the graph again, and come back to this
                // prompt. The writer sees the feedback because HumanGate put it on the state.
                var before = state.Trace.Count;
                BriefWorkflow.RequestChanges(state);
                runs.Save(state);
                WorkflowPrinter.PrintNew(state, before);
                await DriveUntilPauseAsync(client, state, runs);
                continue;
            }

            runs.Save(state);
        }

        WorkflowPrinter.PrintFinal(state, Path.Combine(runs.DirectoryPath, state.RunId + ".json"));
        if (memory is not null && state.Status == "completed")
            Console.WriteLine("Approval was also written to long-term memory. Mode 3 will list it next time.");
    }

    private static WorkflowState? PickRun(RunStore runs)
    {
        var saved = runs.List();
        if (saved.Count == 0)
        {
            Console.WriteLine("No saved runs yet. Start one with option 1.");
            return null;
        }

        Console.WriteLine();
        foreach (var run in saved)
            Console.WriteLine($"  {run.RunId}  {run.Status,-20} {TrimGoal(run.Goal)}");

        Console.Write("Run id: ");
        var id = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(id)) return null;

        var loaded = runs.Load(id);
        if (loaded is null)
            Console.WriteLine("No run with that id.");
        return loaded;
    }

    private static string TrimGoal(string goal) =>
        goal.Length <= 72 ? goal : goal[..72] + "...";
}
