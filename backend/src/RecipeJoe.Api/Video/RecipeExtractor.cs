using System.Reflection;
using System.Text.Json;
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
    IReadOnlyList<string>? IngredientLines,
    IReadOnlyList<string>? Steps
);

/// <summary>Video text → one <see cref="ParsedRecipe"/> per dish, written by an LLM. Owns the prompt, the output schema, validation and the timeout; text in, text out, so <see cref="ParsedRecipe.ImageUrl"/> is always null. Provider details stay in the logs: callers only see the failure kind.</summary>
internal sealed partial class RecipeExtractor(IChatClient chat, IOptions<LlmOptions> options, ILogger<RecipeExtractor> logger)
{
    private static readonly Lazy<string> Prompt = new(LoadPrompt);

    public async Task<Result<IReadOnlyList<ParsedRecipe>, ImportFailure>> ExtractAsync(VideoText video, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.Timeout);

        ExtractionReply reply;
        try
        {
            var response = await chat.GetResponseAsync<ExtractionReply>(
                [new ChatMessage(ChatRole.System, Prompt.Value), new ChatMessage(ChatRole.User, Describe(video))],
                AIJsonUtilities.DefaultOptions,
                new ChatOptions { Temperature = 0.2f },
                useJsonSchemaResponseFormat: true,
                cancellationToken: timeout.Token
            );

            if (!response.TryGetResult(out var parsedReply) || parsedReply?.Recipes is null)
            {
                LogBadReply(response.Text);
                return Fail(ImportFailure.LlmBadOutput);
            }

            reply = parsedReply;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(options.Value.Timeout);
            return Fail(ImportFailure.LlmUnavailable);
        }
        catch (JsonException ex)
        {
            LogBadReply(ex.Message);
            return Fail(ImportFailure.LlmBadOutput);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogProviderFailed(ex);
            return Fail(ImportFailure.LlmUnavailable);
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

    private static string Describe(VideoText video) =>
        $"""
        Titel: {video.Title}

        Beschreibung:
        {video.Description}

        Transkript:
        {video.Transcript}
        """;

    private static string LoadPrompt()
    {
        using var stream = typeof(RecipeExtractor).Assembly.GetManifestResourceStream("RecipeExtractionPrompt.md")
            ?? throw new InvalidOperationException("RecipeExtractionPrompt.md is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [LoggerMessage(LogLevel.Warning, "LLM reply was unusable: {Reply}")]
    private partial void LogBadReply(string reply);

    [LoggerMessage(LogLevel.Warning, "LLM did not answer within {Timeout}")]
    private partial void LogTimedOut(TimeSpan timeout);

    [LoggerMessage(LogLevel.Warning, "LLM provider call failed")]
    private partial void LogProviderFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "Skipped an invalid recipe from the LLM: {Title}")]
    private partial void LogEntrySkipped(string? title);
}
