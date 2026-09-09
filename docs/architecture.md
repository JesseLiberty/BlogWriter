# Architecture

BlogWriter is a Microsoft Agent Framework (MAF) application split into two parts:

1. **Console app** (this repo's root) — orchestrates a bounded revision workflow and never talks to an LLM directly.
2. **Four Azure AI Foundry Hosted Agents** (`HostedAgents/Blogger`, `HostedAgents/Researcher`, `HostedAgents/Author`, `HostedAgents/Reviewer`) — independently deployed, each with its own managed compute and Microsoft Entra ID identity, exposing an OpenAI-compatible `/responses` endpoint.

```mermaid
flowchart LR
    subgraph Local["Console app (Program.cs / BlogWorkflow.cs)"]
        Blogger[BloggerExecutor]
        Researcher[ResearcherExecutor]
        Author[AuthorExecutor]
        Reviewer[ReviewerExecutor]
        Blogger --> Researcher --> Author --> Reviewer
        Reviewer -. "NeedsRevision == true" .-> Author
    end

    subgraph Foundry["Azure AI Foundry (hosted agents, deployed independently via azd)"]
        HBlogger["Blogger agent<br/>(routing)"]
        HResearcher["Researcher agent<br/>(HostedWebSearchTool)"]
        HAuthor["Author agent<br/>(drafts/revises)"]
        HReviewer["Reviewer agent<br/>(approve/reject)"]
    end

    Blogger -. "AsAIAgent().RunAsync()" .-> HBlogger
    Researcher -. "AsAIAgent().RunAsync()" .-> HResearcher
    Author -. "AsAIAgent().RunAsync()" .-> HAuthor
    Reviewer -. "AsAIAgent().RunAsync()" .-> HReviewer
```

## Console app: orchestration only

`Program.cs` never builds an agent in-process. For each of the four roles it calls
`AIProjectClient.AsAIAgent(agentEndpoint, ...)`, which returns a MAF `AIAgent` that
talks to the already-deployed hosted agent's `/responses` endpoint. `BlogWorkflow.cs`
wires those four `AIAgent`s into a MAF `WorkflowBuilder` graph
(`Workflows/BlogExecutors.cs` holds the `[MessageHandler]` executors):

```
Blogger → Researcher → Author → Reviewer
                          ↑        |
                          └── (if state.NeedsRevision) ──┘
```

The revision loop is bounded by `ResearchState.MaxRevisions` — the workflow always
terminates, either on reviewer approval or on hitting the revision cap.

**All agent connectivity goes through MAF (`AsAIAgent` / `AIAgent.RunAsync`) — the
console app never issues a raw HTTP call to an agent endpoint.** This is a hard
constraint of this codebase (see `AGENTS.md`), not just a convention.

## Authentication

Every hop — console app → hosted agent, and hosted agent → Foundry model/tools — uses
Microsoft Entra ID exclusively (`AzureCliCredential` locally, `DefaultAzureCredential` in
the hosted agents). There are no API keys anywhere in this architecture.

## Shared token budget

`TokenCapChatClient.CreateSharedFactory(maxTotalTokens)` produces one `IChatClient`
middleware factory that is passed to every `AsAIAgent(..., clientFactory: ...)` call in
`Program.cs`. All four agents share a single cumulative token counter for the lifetime of
one console-app run (default cap: 40,000 tokens, `MAX_TOTAL_TOKENS`). Exceeding it throws
`TokenCapExceededException`, which unwinds the workflow and exits the app gracefully
instead of continuing to spend tokens.

## Hosted web search (Researcher)

The Researcher hosted agent owns a Foundry-native `HostedWebSearchTool()` — see
`HostedAgents/Researcher/Program.cs`. Web search executes **inside the hosted agent
process**, not in the console app; the console app only sees the final findings text.

## Deployment model

Each `HostedAgents/<Name>` project is deployed independently via `azd` and is
**pre-provisioned** — the console app only references already-deployed hosted agents by
name (`BLOGGER_AGENT_NAME`, `RESEARCHER_AGENT_NAME`, `AUTHOR_AGENT_NAME`,
`REVIEWER_AGENT_NAME`), it never creates or updates them at runtime. See
[deployment.md](deployment.md) for the full `azd` flow and
[configuration.md](configuration.md) for every environment variable involved.

## Known limitation

We're currently seeing more LLM calls than expected during a single workflow run.
It's not yet confirmed whether this is a real extra-call issue or a telemetry
over-count — flagging it here as an open item rather than a resolved one.
