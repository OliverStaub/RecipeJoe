using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace RecipeJoe.Api.Video;

/// <summary>Builds the <see cref="IChatClient"/> for <see cref="LlmOptions.Provider"/>. SDK retries are off: the cook retries a failed Import.</summary>
internal static class LlmClientFactory
{
    /// <summary>One connection pool for all clients (and so no per-Create handler to dispose).</summary>
    private static readonly HttpClient SharedHttp = new(new ProviderErrorHandler(new HttpClientHandler())) { Timeout = Timeout.InfiniteTimeSpan };

    public static IChatClient Create(LlmOptions options) =>
        options.Provider switch
        {
            LlmProvider.OpenRouter => CreateOpenRouter(options),
            _ => throw new NotSupportedException($"LLM provider {options.Provider} is not supported yet."),
        };

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
                // The SDK's own timeout (above) governs; HttpClient's 100 s default must not cut in first.
                Transport = new HttpClientPipelineTransport(SharedHttp),
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
            // The chat library only sends `strict: true` on request, and then also closes every object in the schema.
            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties["strict"] = true;
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
}
