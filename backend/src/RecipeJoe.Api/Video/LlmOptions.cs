using Microsoft.Extensions.Options;

namespace RecipeJoe.Api.Video;

internal enum LlmProvider
{
    OpenRouter,
}

/// <summary>Bound from the "Llm" configuration section and validated on start.</summary>
internal sealed class LlmOptions
{
    public LlmProvider Provider { get; set; } = LlmProvider.OpenRouter;

    /// <summary>Null picks the provider's default model.</summary>
    public string? Model { get; set; }

    /// <summary>Null picks the provider's default endpoint.</summary>
    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Per request, covers a long generation.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(3);

    public string ResolvedModel =>
        Model ?? Provider switch
        {
            LlmProvider.OpenRouter => "google/gemma-4-26b-a4b-it",
            _ => throw new NotSupportedException($"LLM provider {Provider} has no default model."),
        };

    public Uri ResolvedBaseUrl =>
        new(BaseUrl ?? Provider switch
        {
            LlmProvider.OpenRouter => "https://openrouter.ai/api/v1",
            _ => throw new NotSupportedException($"LLM provider {Provider} has no default endpoint."),
        });
}

internal sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        var errors = new List<string>();

        if (!Enum.IsDefined(options.Provider))
        {
            errors.Add("Llm:Provider must be OpenRouter.");
        }

        if (options.Model is not null && string.IsNullOrWhiteSpace(options.Model))
        {
            errors.Add("Llm:Model must not be blank.");
        }

        if (options.BaseUrl is not null && !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("Llm:BaseUrl must be an absolute URL.");
        }

        if (options.Provider == LlmProvider.OpenRouter && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add("Llm:ApiKey is required for OpenRouter.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            errors.Add("Llm:Timeout must be positive.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
