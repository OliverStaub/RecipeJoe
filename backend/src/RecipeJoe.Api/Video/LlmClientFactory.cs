using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace RecipeJoe.Api.Video;

/// <summary>Builds the <see cref="IChatClient"/> for <see cref="LlmOptions.Provider"/>. SDK retries are off: the cook retries a failed Import.</summary>
internal static class LlmClientFactory
{
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
                // The chat library leaves `strict` unset, so providers may treat the schema as a hint.
                if (options.ResponseFormat is ChatResponseFormatJson { Schema: { } schema } json)
                {
                    raw.ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                        json.SchemaName ?? "response",
                        BinaryData.FromString(schema.GetRawText()),
                        json.SchemaDescription,
                        jsonSchemaIsStrict: true
                    );
                }
#pragma warning restore SCME0001
                return raw;
            };

            return base.GetResponseAsync(messages, options, cancellationToken);
        }
    }
}
