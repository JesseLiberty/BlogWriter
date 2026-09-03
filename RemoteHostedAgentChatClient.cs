using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Extensions.AI;

namespace BlogWriter;

/// <summary>
/// <see cref="IChatClient"/> that talks to a single Azure AI Foundry Hosted
/// Agent over its OpenAI-compatible Responses protocol endpoint
/// (<c>{project_endpoint}/agents/{name}/endpoint/protocols/openai/responses</c>),
/// authenticated with a Microsoft Entra ID bearer token (no API keys — per the
/// project's hard constraint on Entra-only auth).
///
/// This is a minimal, self-contained client: it posts the conversation as a
/// single <c>input</c> string (system/user turns concatenated) and reads back
/// <c>output_text</c>. It composes normally with the rest of the
/// <see cref="IChatClient"/> pipeline (function invocation, OpenTelemetry,
/// <see cref="TokenCapChatClient"/>) since those only depend on the
/// <see cref="IChatClient"/> abstraction, not the transport.
///
/// Streaming is not implemented against the hosted protocol yet; it falls back
/// to a single update built from the non-streaming response.
/// </summary>
public sealed class RemoteHostedAgentChatClient : IChatClient
{
    // Default Entra ID scope for Foundry Agent Service data-plane calls.
    // Confirm this against the target Foundry resource before production use —
    // some deployments may require "https://cognitiveservices.azure.com/.default".
    public const string DefaultScope = "https://ai.azure.com/.default";

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly string _scope;
    private readonly Uri _responsesEndpoint;

    /// <param name="httpClient">Shared HttpClient; caller owns disposal.</param>
    /// <param name="credential">Entra ID credential, e.g. <c>DefaultAzureCredential</c>.</param>
    /// <param name="projectEndpoint">Foundry project endpoint, e.g. https://&lt;account&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</param>
    /// <param name="hostedAgentName">Name of the deployed hosted agent (pre-provisioned via azd; not created at runtime).</param>
    /// <param name="scope">Entra ID token scope; defaults to <see cref="DefaultScope"/>.</param>
    public RemoteHostedAgentChatClient(
        HttpClient httpClient,
        TokenCredential credential,
        Uri projectEndpoint,
        string hostedAgentName,
        string scope = DefaultScope)
    {
        _httpClient = httpClient;
        _credential = credential;
        _scope = scope;
        _responsesEndpoint = new Uri(
            projectEndpoint,
            $"agents/{hostedAgentName}/endpoint/protocols/openai/responses");
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        string input = BuildInput(messages);

        using HttpRequestMessage request = new(HttpMethod.Post, _responsesEndpoint)
        {
            Content = JsonContent.Create(new HostedAgentRequest(input, options?.MaxOutputTokens, options?.Temperature)),
        };

        AccessToken token = await _credential.GetTokenAsync(new TokenRequestContext([_scope]), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        HostedAgentResponse? payload = await response.Content.ReadFromJsonAsync<HostedAgentResponse>(cancellationToken: cancellationToken);
        string text = payload?.OutputText ?? string.Empty;

        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        if (payload?.Usage is { } usage)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = usage.InputTokens,
                OutputTokenCount = usage.OutputTokens,
                TotalTokenCount = usage.TotalTokens,
            };
        }

        return chatResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // No native streaming support against the hosted Responses endpoint yet;
        // surface the full response as a single update.
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatMessage message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Contents);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        // _httpClient is owned by the caller (shared across agents); nothing to dispose here.
    }

    private static string BuildInput(IEnumerable<ChatMessage> messages)
    {
        // Concatenate system/user turns into the single "input" string the
        // hosted Responses endpoint accepts (see the /responses curl sample in
        // the Foundry Hosted Agents docs). The hosted agent's own baked-in
        // instructions still apply server-side; this preserves the same
        // client-supplied Instructions/user-message behaviour the app relied on
        // pre-migration.
        return string.Join("\n\n", messages.Select(m => $"[{m.Role}] {m.Text}"));
    }

    private sealed record HostedAgentRequest(
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("max_output_tokens")] int? MaxOutputTokens,
        [property: JsonPropertyName("temperature")] float? Temperature);

    private sealed record HostedAgentResponse(
        [property: JsonPropertyName("output_text")] string? OutputText,
        [property: JsonPropertyName("usage")] HostedAgentUsage? Usage);

    private sealed record HostedAgentUsage(
        [property: JsonPropertyName("input_tokens")] int? InputTokens,
        [property: JsonPropertyName("output_tokens")] int? OutputTokens,
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);
}
