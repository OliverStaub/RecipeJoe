using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OpenAI;
using OpenAI.Chat;

namespace RecipeJoe.Api.Video;

/// <summary>Builds the <see cref="IChatClient"/> for <see cref="LlmOptions.Provider"/>. SDK retries are off: the cook retries a failed Import.</summary>
internal static class LlmClientFactory
{
    /// <param name="primaryHandler">Replaces the network transport of the Ollama client; tests only.</param>
    public static IChatClient Create(LlmOptions options, HttpMessageHandler? primaryHandler = null) =>
        options.Provider switch
        {
            LlmProvider.Ollama => CreateOllama(options, primaryHandler),
            LlmProvider.OpenRouter => CreateOpenRouter(options),
            _ => throw new NotSupportedException($"LLM provider {options.Provider} is not supported yet."),
        };

    private static OllamaChatClient CreateOllama(LlmOptions options, HttpMessageHandler? primaryHandler)
    {
        // The extractor owns the timeout; HttpClient's 100 s default would cut a long local generation short.
        var http = new HttpClient(new NoTruncateHandler(primaryHandler ?? new HttpClientHandler())) { BaseAddress = options.ResolvedBaseUrl, Timeout = options.Timeout + TimeSpan.FromSeconds(10) };
        return new OllamaChatClient(new OllamaApiClient(http, options.ResolvedModel), options.ContextTokens);
    }

    private static RequireParametersChatClient CreateOpenRouter(LlmOptions options)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey!),
            new OpenAIClientOptions
            {
                Endpoint = options.ResolvedBaseUrl,
                RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
                // The extractor owns the timeout; the SDK's 100 s default would cut a long generation short.
                NetworkTimeout = options.Timeout + TimeSpan.FromSeconds(10),
            }
        );

        return new RequireParametersChatClient(client.GetChatClient(options.ResolvedModel).AsIChatClient());
    }

    /// <summary>OpenRouter routes to any upstream provider by default, including ones that ignore <c>response_format</c>; <c>provider.require_parameters</c> restricts routing to providers that honour every parameter we send.</summary>
    private sealed class RequireParametersChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            options = options?.Clone() ?? new ChatOptions();
            var previous = options.RawRepresentationFactory;
            options.RawRepresentationFactory = client =>
            {
                var raw = previous?.Invoke(client) as ChatCompletionOptions ?? new ChatCompletionOptions();
#pragma warning disable SCME0001 // JsonPatch is the SDK's only way to add a non-OpenAI request field.
                raw.Patch.Set("$.provider"u8, BinaryData.FromString("""{"require_parameters":true}"""));
#pragma warning restore SCME0001
                return raw;
            };

            return base.GetResponseAsync(messages, options, cancellationToken);
        }
    }

    /// <summary>Ollama silently drops the front of an over-long prompt (system prompt and title first) and writes a wrong Recipe. <c>truncate:false</c> turns that into a 400 <c>exceed_context_size_error</c>. OllamaSharp has no such field, so it is added to the <c>/api/chat</c> body here.</summary>
    private sealed class NoTruncateHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath.EndsWith("/api/chat", StringComparison.Ordinal) == true && request.Content is not null)
            {
                var body = JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
                body["truncate"] = false;
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>Native Ollama API: <c>think:false</c> because Gemma's reasoning tokens would eat the generation budget, and a long <c>keep_alive</c> so the model stays loaded between Imports.</summary>
    private sealed class OllamaChatClient(OllamaApiClient inner, int contextTokens) : DelegatingChatClient(inner)
    {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            options = options?.Clone() ?? new ChatOptions();
            var previous = options.RawRepresentationFactory;
            options.RawRepresentationFactory = client =>
            {
                var raw = previous?.Invoke(client) as ChatRequest ?? new ChatRequest();
                raw.Think = false;
                raw.KeepAlive = "30m";
                raw.Options ??= new OllamaSharp.Models.RequestOptions();
                raw.Options.NumCtx = contextTokens;
                return raw;
            };

            return base.GetResponseAsync(messages, options, cancellationToken);
        }
    }
}
