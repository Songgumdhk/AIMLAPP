# Chapter 4 — Agentic Workflows

> **Goal:** Stop thinking of an agent as one chat loop, and start designing a
> **reliable system**: a plan, a shared state, memory that you choose to keep,
> a place where a human can say no, and more than one agent when the work
> splits cleanly.
>
> You already know what an agent is. This chapter is about how to design a
> *reliable* agentic system.

---

## Table of Contents

1. [What you already have](#1-what-you-already-have)
2. [A workflow is a graph](#2-a-workflow-is-a-graph)
3. [State machines, branches, and loops](#3-state-machines-branches-and-loops)
4. [Planning](#4-planning)
5. [State](#5-state)
6. [Memory](#6-memory)
7. [Human-in-the-loop](#7-human-in-the-loop)
8. [Multi-agent](#8-multi-agent)
9. [Persistence](#9-persistence)
10. [Putting it all together](#10-putting-it-all-together)
11. [The same ideas in a framework](#11-the-same-ideas-in-a-framework)
12. [Common Pitfalls](#12-common-pitfalls)
13. [Practice Exercises](#13-practice-exercises)

> **Settings for this chapter** live in `appsettings.json`. `OpenAI:ChatModel`
> runs every agent. The loop caps that `WorkflowLimits` exposes are read from
> the `AgenticWorkflows` section:
>
> | Setting | Default | Meaning |
> | --- | --- | --- |
> | `ReviewPassScore` | 7 | Reviewer score (1–10) that counts as a pass |
> | `MaxRevisions` | 2 | Rewrites allowed after a failed review |
> | `MaxNodeVisits` | 16 | Node cap for the code-routed graph (option 6) |
> | `MaxAgentHops` | 8 | Hand-off cap for the model supervisor (option 5) |

---

## 1. What you already have

Chapter 1's function-calling loop is an agent:

```
messages + tools → model → maybe a tool → feed the result back → repeat
```

That loop is enough when the task is "answer this, calling tools as you go."
It gets unreliable when the task is a **process**:

- gather facts, then write, then check the writing
- stop before anything is sent
- remember a preference next week
- hand the draft to a different role than the one that researched it

A workflow makes those steps **explicit**. Each step is a node. The thing
that moves between nodes is **state**, not a growing chat transcript you
hope the model will interpret correctly.

```
User
 ↓
Plan          ← the model writes steps, your code owns the list
 ↓
Route         ← code picks the next node from that list
 ├── Research agent
 ├── Writer agent
 └── Reviewer agent
 ↓
Human         ← the graph pauses on purpose
 ↓
Respond
```

---

## 2. A workflow is a graph

Three words cover the whole chapter:

| Word     | Meaning in this demo                                              |
| -------- | ----------------------------------------------------------------- |
| **Node** | A function that reads `WorkflowState` and updates it.            |
| **Edge** | The decision of which node runs next.                             |
| **State**| The one object every node shares.                                 |

Chapter 1 has one node (the chat loop) and one edge ("call the model again").
This chapter has several nodes and edges that can branch or loop.

The onboarding-brief graph in menu option **6**:

```
plan → route → research → route → draft → route → review
                 ▲                               │
                 │         score < 7             │
                 └──────── and revisions left ───┘
route (nothing pending) → hitl → pause until a person answers
```

`route` is boring on purpose. It looks at `PlanStep.Status` and jumps to
the first step that is still `pending`. When a reviewer rejects the draft,
the code sets `draft` and `review` back to `pending`. The next visit to
`route` sends the work to the writer again. That is a loop, and it is
visible in the trace.

---

## 3. State machines, branches, and loops

Menu option **2** runs a ticket graph with **no model**, so you can see the
mechanism before any prompt is involved.

```
intake → classify → billing    → resolve → close
                 → technical   → resolve → close
                 → general     → resolve → close
                 → escalate
```

`classify` is **conditional routing**. The next node depends on
`state.Category`, which an earlier node wrote.

`resolve` is a **loop**. If you fail the first attempt, it returns
`"classify"` instead of `"close"`. The `while` loop in
`TicketStateMachine` is the whole engine:

```csharp
node = node switch
{
    "classify" => Classify(state, category),
    "resolve"  => Resolve(state, failFirstAttempt),
    "close"    => Finish(state, "closed"),
    _ => null   // null means the graph ended
};
```

A node returns the name of the next node. That return value is the edge.
The same pattern is what `BriefWorkflow.StepAsync` does once a model is
sitting inside some of the nodes.

**Cap the loop.** Option 2 stops after 12 visits. Option 6 stops after
`WorkflowLimits.MaxNodeVisits` (`AgenticWorkflows:MaxNodeVisits` in
`appsettings.json`). A graph that can jump backwards will otherwise run
until the API budget does.

---

## 4. Planning

Planning means: **ask the model for a list of steps, then execute that
list with your code.**

Menu option **1** stops after the list. Nothing is searched, drafted, or
sent. Read the steps and check that a human can tell what will happen.

The planner must answer with JSON shaped like:

```json
{
  "steps": [
    { "action": "research", "instruction": "Find the vacation and security rules." },
    { "action": "draft", "instruction": "Write the onboarding brief from those notes." },
    { "action": "review", "instruction": "Score the brief against the notes." }
  ]
}
```

`action` is not free text. The graph only knows `research`, `draft`, and
`review`. `PlannerAgent` drops anything else and, if a stage is missing,
inserts a default step. The plan you see in option 1 is the plan option 6
will actually run.

Why bother, if the model could just call tools in a loop?

| Chat loop                         | Explicit plan                                      |
| --------------------------------- | -------------------------------------------------- |
| The next action is a surprise.    | You can show the steps before they run.           |
| Hard to resume after a crash.     | The plan is data on the state object.             |
| "Did it review the draft?"        | Look for a `review` step whose status is `done`.  |

The model is good at proposing the steps. Your code should be the thing
that marks them done.

---

## 5. State

`WorkflowState` is the blackboard. Agents do not pass essays to each other
through hidden chat history. They write fields:

| Field            | Who writes it        | Who reads it                          |
| ---------------- | -------------------- | ------------------------------------- |
| `Goal`           | The user             | Every agent                           |
| `Plan`           | Planner              | `route`                               |
| `Scratchpad`     | Researcher           | Writer, reviewer                      |
| `MemoryFacts`    | Loaded at start      | Writer                                |
| `Draft`          | Writer               | Reviewer, human                       |
| `ReviewScore`    | Reviewer             | The review edge (`ApplyReview`)       |
| `HumanFeedback`  | The human            | Writer, on the next revision          |
| `Status`         | The engine           | The loop that decides whether to stop |
| `Trace`          | Every node           | You, in the console and in the JSON   |

A new draft sets `ReviewScore` back to `0`. Otherwise a stale "8" would
let the graph skip review. State has to stay true after every node, not
only at the end.

Open the run JSON (the path is printed when a run finishes). You should be
able to point at each field and name the node that wrote it. If you cannot,
the state is hiding something, and the workflow will be hard to debug.

---

## 6. Memory

There are two stores, and mixing them up is how agents "forget" or "leak."

**Short-term** is `WorkflowState.Scratchpad`.
The researcher appends notes. The writer reads them later in the **same
run**. They are saved in the run JSON so you can resume, and they are not
copied into the preference file.

**Long-term** is `MemoryStore` (`agent-memory/facts.json` under the build
output). It survives after the process exits. Menu option **3** lets you
write a fact, then draft a note. The draft can see both:

- the fact you saved (still there after a restart)
- a scratchpad line about Friday's security training (used once, not saved
  as a preference)

Option 6 copies the long-term facts onto `state.MemoryFacts` when the run
starts, so the checkpoint shows what the writer was allowed to see. On
approve, it writes one history line back (`history: published a brief...`).
That is episodic memory: a record that something happened, not a dump of
the whole draft.

Rule used in this demo:

```
Scratchpad  = notes for this task
facts.json  = preferences and short history you chose to keep
```

Do not persist the entire transcript and call it memory. The next run will
drown in it.

---

## 7. Human-in-the-loop

Chapter 1 asked "approve this tool call?" inside the chat loop.
A workflow pause is stronger: **the node sets `Status` to
`waiting_for_human` and the engine stops.** No further model call happens
until a person answers.

Menu option **4** drafts an email to HR and then waits:

| Answer   | What the graph does                                      |
| -------- | -------------------------------------------------------- |
| approve  | `Status = completed`. The email is considered sent.      |
| reject   | `Status = rejected`. The draft stays in the checkpoint.  |
| edit     | Feedback is stored, `draft` is set back to `pending`, the writer runs again, then the graph waits again. |

The pause is the feature. A model that "sends" inside the same turn that
it drafted has no place for a person to catch a bad email.

The checkpoint is written **before** the prompt. You can stop the process
at that prompt and resume from menu option 6 → "Resume a saved run".
The run id and the `waiting_for_human` status are in the JSON file.

---

## 8. Multi-agent

An agent here is a model call with a **role**, reading and writing the
same state:

| Agent       | Job                                      | Allowed to do                         |
| ----------- | ---------------------------------------- | ------------------------------------- |
| Researcher  | Search the handbook, write bullets.      | Append to `Scratchpad`.               |
| Writer      | Produce the deliverable.                 | Set `Draft`. Clear the old score.     |
| Reviewer    | Score 1–10 and leave one sentence.       | Set `ReviewScore` and `ReviewNotes`.  |
| Supervisor  | Pick the next role.                      | Choose a name. It does not write the draft. |
| Planner     | Propose steps.                           | Fill `Plan`. It does not execute them.|

The handbook search is a normal method, `PolicyCorpus.Search`. The
researcher calls it the way Chapter 1 called `search_database`. This
chapter keeps the search dumb (keyword overlap) so the lesson stays on
the graph. Swap in Chapter 3's retriever later if you want real RAG
inside the research node.

Two ways to decide the next agent:

**Code router (option 6).** The plan says `research`, so the next node is
the researcher. The reviewer does not get to decide that its own score
"passes." `BriefWorkflow.ApplyReview` does:

```csharp
if (score < WorkflowLimits.ReviewPassScore && revisions < WorkflowLimits.MaxRevisions)
    reopen draft and review;
else
    mark review done and fall through to the human;
```

**Model supervisor (option 5).** A separate call returns
`researcher | writer | reviewer | done`. This is flexible and easier to
fool. The hop cap (`MaxAgentHops`) is what stops a supervisor that keeps
picking the reviewer. Run option 5 and option 6 on the same goal and
compare the traces. The code router's trace should be predictable. The
supervisor's trace will wander.

Use a supervisor when the next step genuinely depends on judgment you do
not want to encode. Use a code router when the process is a checklist.
Production systems usually do the second, and call a model **inside**
the nodes.

The shape from the roadmap, with a supervisor in front:

```
                  ┌── Researcher
                  │
User → Supervisor ├── Writer
                  │
                  └── Reviewer
```

Specialists do not talk to each other directly. They talk through state.
That is what makes the handoff debuggable.

---

## 9. Persistence

`RunStore` writes the whole `WorkflowState` as JSON after every node
(`agent-runs/{runId}.json` under the build output, which is gitignored).

That file is the difference between a demo that dies on Ctrl+C and a
workflow you can resume:

```
node finishes
    → state.Status / state.CurrentNode updated
    → JSON flushed
    → next node, or stop
```

Resume loads that file and continues from `CurrentNode`. A run left in
`waiting_for_human` opens the approval prompt again. A run left in
`running` (the process died mid-graph) continues the loop.

Checkpoints store **state**, not "whatever was on the console." If a field
matters for the next node, it has to be on `WorkflowState` before `Save`.

---

## 10. Putting it all together

Menu option **6** is the chapter in one run.

```
Goal
  │
  ├─ load long-term facts onto the state
  │
  ├─ plan          planner writes research → draft → review
  │
  ├─ route / act   researcher fills the scratchpad
  │                writer drafts from scratchpad + facts
  │                reviewer scores
  │                low score loops back to the writer (max 2 revisions)
  │
  ├─ hitl          you approve, edit, or reject
  │
  └─ on approve    one history line is written to long-term memory
```

A suggested goal, because the handbook actually contains these facts:

```
Write an onboarding brief for a new engineer covering vacation and security.
```

You should see, in order:

1. A plan whose actions are `research`, `draft`, `review`.
2. A scratchpad line grounded in the handbook (15 vacation days, Friday
   security training, mentor, VPN).
3. A draft that uses those notes.
4. A score, and a second draft only if the score is below 7 (`AgenticWorkflows:ReviewPassScore`).
5. A pause. Nothing is "published" until you type `a`.
6. A path to the JSON file. Open it. `status`, `plan`, `scratchpad`,
   `draft`, and `trace` should match what the console showed.

---

## 11. The same ideas in a framework

This demo is a small engine so every edge is a line of C# you can read.
The same picture shows up in two places people will name in interviews:

| This demo                         | LangGraph (concepts)        | `Microsoft.Agents.AI.Workflows` (already in this repo) |
| --------------------------------- | --------------------------- | ------------------------------------------------------ |
| `WorkflowState`                   | Graph state                 | State flowing between executors                        |
| A method like `ResearcherAgent`   | A node                      | An `Executor`                                          |
| Returning the next node name      | An edge, often conditional  | An `Edge` between executors                            |
| `HumanGate` + `waiting_for_human` | `interrupt`                 | A `RequestPort` the host answers                       |
| `RunStore`                        | A checkpointer              | Checkpoint / `RestoreCheckpointAsync`                  |

The LangGraph course under "Further learning" in the README is worth it for the vocabulary. You do not
need to ship Python to use the ideas. When a library starts hiding the
graph, come back to option 2 and option 6 and ask: where is the state,
who chooses the next node, and where does a human get to stop it?

---

## 12. Common Pitfalls

| Pitfall                                              | What to do instead                                                                 |
| ---------------------------------------------------- | ---------------------------------------------------------------------------------- |
| One giant prompt that "plans and acts and reviews"   | Split nodes. Put the plan on the state where you can see it.                      |
| Letting the reviewer decide it passed               | The score is data. A code edge compares it to a threshold.                        |
| An unbounded revise loop                            | `MaxRevisions`, `MaxNodeVisits`, `MaxAgentHops`. Same lesson as `MAX_TURNS`.     |
| Treating the chat log as the only memory            | Separate scratchpad (this task) from facts you explicitly save.                   |
| Saving every token forever                          | Persist the fields the next node needs. Keep long-term facts short.               |
| Sending email inside the writer node                | Writer produces a draft. A later node, after a human, performs the side effect.   |
| Supervisor with no hop cap                          | A bad `next` value will cycle. Cap it and log the reason.                         |
| Resuming from the console scrollback                | Resume from the checkpoint. The console is not the state.                         |
| Specialists calling each other                      | They write state. A router or supervisor reads it and picks the next role.        |

---

## 13. Practice Exercises

All of these run from menu option **8**.

### Exercise 1 — Read a plan before anything runs

Option **1**. Goal:

```
Write an onboarding brief for a new engineer covering vacation and security.
```

Check that you get `research`, then `draft`, then `review`, and that each
instruction is something a person could follow. Then run option **6** with
the same goal and confirm the trace starts with those actions.

### Exercise 2 — See a branch and a loop with no model

Option **2**.

- Category `billing`, fail the first attempt: the trace goes
  `resolve → classify → billing → resolve → close`. `attempt` becomes 2.
- Category `other`: `classify` goes straight to `escalate`. No desk runs.

That arrow is the entire idea of conditional routing.

### Exercise 3 — Two kinds of memory

Option **3**.

1. Remember: `I prefer four short bullet points.`
2. Choose "write a welcome note." The Friday training line is only on the
   scratchpad. The preference is in `facts.json`.
3. Stop the app, start it, open option 3 again. The preference is still
   listed. The Friday sentence is not in the file.
4. Write another note. It should still follow the bullet preference.

### Exercise 4 — Pause on purpose

Option **4**.

- Type `r` and confirm the run ends as `rejected` with the draft still in
  the JSON.
- Run it again, type `e`, and ask for a shorter subject line. The trace
  should show `[hitl] changes requested`, then another `[draft]`, then
  another pause.
- On a third run, type `a`. Status becomes `completed`.

### Exercise 5 — Supervisor vs checklist

Run option **5** and option **6** with the same onboarding goal.

- Option 5: each hop is a supervisor decision. Count the model calls. See
  whether it ever skips research or reviews twice.
- Option 6: the route lines follow the plan. A low score loops to `draft`
  because of `ApplyReview`, not because a model felt like it.

### Exercise 6 — Kill it and resume

Option **6**, start a new goal, and wait until the approval prompt.
The state file path is in the `status=waiting_for_human` snapshot (and
under `agent-runs` in the build output).

Stop the process at the approval prompt. The console prints `checkpoint:` with
the JSON path, and the run is already saved. Start the app, option 8 →
option 6 → "Resume a saved run", and enter the run id. You should get the
same draft and the same approve / edit / reject prompt, without paying for
the plan and research calls again.

---

## What's Next?

Once you can point at a trace and explain every hop, the next chapter
is **AI Evaluation**: how you know the retrieval, the draft,
and the review score are any good when you are not sitting at the console
watching them. That chapter is `05-AIEvaluation.md`, menu option **9**.
