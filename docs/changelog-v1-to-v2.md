# Changelog: v1 → current (hosted agents)

This summarizes what changed in the `hostedAgents` branch versus the original
in-process version of BlogWriter, for anyone picking the project back up.

## 1. Agents moved from in-process to independently deployed Foundry Hosted Agents

**Before:** Blogger/Researcher/Author/Reviewer were built and run in-process inside the
console app.

**Now:** each agent is its own project under `HostedAgents/<Name>/`, deployed
independently via `azd` to Azure AI Foundry Agent Service, with its own compute,
Entra ID identity, and OpenAI-compatible `/responses` endpoint. The console app only
references them by name (`AsAIAgent(agentEndpoint, ...)`) — it never builds, provisions,
or updates them at runtime. See [architecture.md](architecture.md).

## 2. Researcher's web search moved from a custom Tavily tool to Foundry-native `HostedWebSearchTool`

**Before:** the Researcher agent called a custom Tavily HTTP function/tool with a
hand-written function schema. This schema was incompatible with the hosted Responses
endpoint and caused HTTP 400 failures during the migration to hosted agents.

**Now:** the Researcher hosted agent uses Foundry's built-in `HostedWebSearchTool()`
(see `HostedAgents/Researcher/Program.cs`). Search executes server-side inside the
hosted agent process; there's no Tavily API key or custom tool schema to maintain.

## 3. Per-call token limit replaced with a shared, cumulative token budget

**Before:** a per-call `MAX_OUTPUT_TOKENS` request option was sent on every model call.
This option isn't supported by the Foundry Hosted Agent Responses endpoints and was
dead weight even before that.

**Now:** `TokenCapChatClient.CreateSharedFactory(maxTotalTokens)` wraps all four agents
via MAF's `clientFactory` hook, tracking one cumulative token count across the whole
workflow run (`MAX_TOTAL_TOKENS`, default 40,000). Exceeding it throws
`TokenCapExceededException` and the app shuts down gracefully instead of continuing to
spend tokens.

## 4. All agent connectivity now goes through Microsoft Agent Framework — never raw HTTP

**Before/during migration:** there was a temptation (and at one point, an attempt) to
call agent endpoints directly over HTTP.

**Now:** every agent call is `AIProjectClient.AsAIAgent(...)` + `AIAgent.RunAsync(...)`.
This is enforced as a hard constraint in `AGENTS.md`/MAF Doctor guidance, not just a
style preference — direct HTTP calls to agent endpoints should be treated as a bug if
seen again.

## Known limitation carried forward

The original README's "Known Issues" note — more LLM calls than expected per run,
possibly a telemetry over-count rather than a real issue — is still open. See
[architecture.md](architecture.md#known-limitation).
