using System.Text.Json;

namespace RecipeJoe.Api.Video;

/// <summary>OpenRouter reports an upstream failure inside an HTTP 200 reply: a top-level <c>error</c> object or <c>finish_reason: "error"</c>.</summary>
internal sealed class ProviderErrorException(string message, string? provider) : Exception(message)
{
    public string? Provider { get; } = provider;
}

/// <summary>
/// Turns such replies into a <see cref="ProviderErrorException"/> carrying the provider's own message. It sits on the HTTP transport because the OpenAI SDK's
/// finish-reason enum is closed (it throws a bare "Unknown ChatFinishReason" before anyone sees the body) and a pipeline policy cannot replace the response body.
/// </summary>
internal sealed class ProviderErrorHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType is not ("application/json" or "application/problem+json"))
        {
            return response;
        }

        await response.Content.LoadIntoBufferAsync(cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (FindError(body) is { } error)
        {
            response.Dispose();
            throw error;
        }

        return response;
    }

    private static ProviderErrorException? FindError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var provider = root.TryGetProperty("provider", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            if (root.TryGetProperty("error", out var top) && top.ValueKind == JsonValueKind.Object)
            {
                return Describe(top, provider);
            }

            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String && reason.GetString() == "error")
                    {
                        return Describe(choice.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? e : default, provider);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not ours to judge: the SDK reports unparseable replies.
        }

        return null;
    }

    private static ProviderErrorException Describe(JsonElement error, string? provider)
    {
        string? message = null;
        string? code = null;
        if (error.ValueKind == JsonValueKind.Object)
        {
            message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            code = error.TryGetProperty("code", out var c) ? c.ToString() : null;
            if (error.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object
                && meta.TryGetProperty("provider_name", out var name) && name.ValueKind == JsonValueKind.String)
            {
                provider ??= name.GetString();
            }
        }

        return new ProviderErrorException($"{message ?? "finish_reason \"error\" without a message"}{(code is null ? "" : $" (code {code})")}", provider);
    }
}
