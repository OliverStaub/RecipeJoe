using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Video;

/// <summary>What the LLM fills in. Minutes are integers so the model never has to write ISO 8601; the JSON schema is generated from these records, so schema and parser can't drift.</summary>
internal sealed record ExtractionReply(IReadOnlyList<ExtractedRecipe> Recipes);

internal sealed record ExtractedRecipe(
    string? Title,
    string? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    int? TotalMinutes,
    [property: Description("One plain string per ingredient, e.g. \"250 g Spaghetti\". Never an object.")][property: MinLength(1)] IReadOnlyList<string> IngredientLines,
    [property: Description("One plain string per preparation step, in order. Never an object.")][property: MinLength(1)] IReadOnlyList<string> Steps
);

/// <summary>The pre-call's answer: does the video text hold a cookable recipe?</summary>
internal sealed record RecipeCheckReply(
    [property: JsonRequired][property: Description("True only if the text contains ingredients or preparation of at least one dish; false for mere dish names.")] bool ContainsRecipe
);

/// <summary>Video text → one <see cref="ParsedRecipe"/> per dish, written by an LLM. Asks a cheap yes/no "is there a recipe" question first, then extracts. Owns the prompts, the output schemas, validation and the timeout; text in, text out, so <see cref="ParsedRecipe.ImageUrl"/> is always null. Provider details stay in the logs: callers only see the failure kind.</summary>
internal sealed partial class RecipeExtractor(IChatClient chat, IOptions<LlmOptions> options, ILogger<RecipeExtractor> logger)
{
    private const string ExtractionTask = "Schreibe für jedes Gericht, dessen Zutaten und Zubereitung im Text stehen, ein Rezept. Gibt es keines, antworte mit {\"recipes\": []}. Antworte nur als JSON im beschriebenen Format.";
    private const string CheckTask = "Entscheide, ob der Text ein kochbares Rezept enthält. Antworte nur als JSON im beschriebenen Format.";

    private static readonly Lazy<string> Prompt = new(() => LoadPrompt("RecipeExtractionPrompt.md"));
    private static readonly Lazy<string> CheckPrompt = new(() => LoadPrompt("RecipeCheckPrompt.md"));

    public async Task<Result<IReadOnlyList<ParsedRecipe>, ImportFailure>> ExtractAsync(VideoText video, CancellationToken cancellationToken)
    {
        var check = await AskAsync<RecipeCheckReply>(CheckPrompt.Value, Describe(video, CheckTask), options.Value.RecipeCheckModel, cancellationToken);
        if (!check.IsSuccess)
        {
            return Fail(check.Failure);
        }

        if (!check.Value.ContainsRecipe)
        {
            return Fail(ImportFailure.NoRecipe);
        }

        var asked = await AskAsync<ExtractionReply>(Prompt.Value, Describe(video, ExtractionTask), null, cancellationToken);
        if (!asked.IsSuccess)
        {
            return Fail(asked.Failure);
        }

        var reply = asked.Value;
        if (reply.Recipes is null)
        {
            LogBadReply("no recipes property");
            return Fail(ImportFailure.LlmBadOutput);
        }

        if (reply.Recipes.Count == 0)
        {
            return Fail(ImportFailure.NoRecipe);
        }

        var valid = new List<ParsedRecipe>();
        foreach (var entry in reply.Recipes)
        {
            if (Validate(entry) is { } parsed)
            {
                valid.Add(parsed);
            }
            else
            {
                LogEntrySkipped(entry?.Title);
            }
        }

        return valid.Count == 0 ? Fail(ImportFailure.LlmBadOutput) : Result<IReadOnlyList<ParsedRecipe>, ImportFailure>.Ok(valid);
    }

    /// <summary>One structured call; every provider problem becomes a failure kind. <paramref name="model"/> null keeps the client's model.</summary>
    private async Task<Result<T, ImportFailure>> AskAsync<T>(string system, string user, string? model, CancellationToken cancellationToken)
        where T : class
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.Timeout);

        try
        {
            var response = await chat.GetResponseAsync<T>(
                [new ChatMessage(ChatRole.System, system), new ChatMessage(ChatRole.User, user)],
                AIJsonUtilities.DefaultOptions,
                new ChatOptions { Temperature = 0.2f, ModelId = model },
                useJsonSchemaResponseFormat: true,
                cancellationToken: timeout.Token
            );

            LogServedBy(typeof(T).Name, response.ModelId ?? model ?? "default", LlmClientFactory.ServingProvider(response) ?? "unknown");

            if (!response.TryGetResult(out var parsed) || parsed is null)
            {
                LogBadReply(response.Text);
                return Result<T, ImportFailure>.Fail(ImportFailure.LlmBadOutput);
            }

            return Result<T, ImportFailure>.Ok(parsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(options.Value.Timeout);
            return Result<T, ImportFailure>.Fail(ImportFailure.LlmUnavailable);
        }
        catch (JsonException ex)
        {
            LogBadReply(ex.Message);
            return Result<T, ImportFailure>.Fail(ImportFailure.LlmBadOutput);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && FindProviderError(ex) is { } providerError)
        {
            LogProviderError(providerError.Provider ?? "unknown", providerError.Message);
            return Result<T, ImportFailure>.Fail(ImportFailure.LlmUnavailable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && IsContextOverflow(ex))
        {
            LogContextOverflow(ex.Message);
            return Result<T, ImportFailure>.Fail(ImportFailure.VideoTooLong);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogProviderFailed(ex);
            return Result<T, ImportFailure>.Fail(ImportFailure.LlmUnavailable);
        }
    }

    private static ProviderErrorException? FindProviderError(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is ProviderErrorException found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>OpenRouter words a context overflow as "maximum context length". Matched loosely: the overflow reply is documented, not reproduced.</summary>
    private static bool IsContextOverflow(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("context length", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Result<IReadOnlyList<ParsedRecipe>, ImportFailure> Fail(ImportFailure failure) =>
        Result<IReadOnlyList<ParsedRecipe>, ImportFailure>.Fail(failure);

    private static ParsedRecipe? Validate(ExtractedRecipe? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Title))
        {
            return null;
        }

        var ingredients = Clean(entry.IngredientLines);
        var steps = Clean(entry.Steps);
        if (ingredients.Count == 0 || steps.Count == 0)
        {
            return null;
        }

        return new ParsedRecipe(
            entry.Title.Trim(),
            string.IsNullOrWhiteSpace(entry.Servings) ? null : entry.Servings.Trim(),
            Minutes(entry.PrepMinutes),
            Minutes(entry.CookMinutes),
            Minutes(entry.TotalMinutes),
            ingredients,
            steps,
            null
        );
    }

    private static List<string> Clean(IReadOnlyList<string>? lines) =>
        [.. (lines ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim())];

    private static TimeSpan? Minutes(int? minutes) => minutes is > 0 ? TimeSpan.FromMinutes(minutes.Value) : null;

    private static string Describe(VideoText video, string task) =>
        $$"""
        Titel: {{video.Title}}

        Beschreibung:
        {{video.Description}}

        Transkript:
        {{video.Transcript}}

        Basierend auf dem Text oben: {{task}}
        """;

    private static string LoadPrompt(string name)
    {
        using var stream = typeof(RecipeExtractor).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [LoggerMessage(LogLevel.Information, "{Call} answered by model {Model} via provider {Provider}")]
    private partial void LogServedBy(string call, string model, string provider);

    [LoggerMessage(LogLevel.Warning, "LLM reply was unusable: {Reply}")]
    private partial void LogBadReply(string reply);

    [LoggerMessage(LogLevel.Warning, "LLM did not answer within {Timeout}")]
    private partial void LogTimedOut(TimeSpan timeout);

    [LoggerMessage(LogLevel.Warning, "Video text exceeds the LLM context window: {Message}")]
    private partial void LogContextOverflow(string message);

    [LoggerMessage(LogLevel.Warning, "LLM provider {Provider} reported an error: {Message}")]
    private partial void LogProviderError(string provider, string message);

    [LoggerMessage(LogLevel.Warning, "LLM provider call failed")]
    private partial void LogProviderFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "Skipped an invalid recipe from the LLM: {Title}")]
    private partial void LogEntrySkipped(string? title);
}
