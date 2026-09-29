using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using AngleSharp.Html.Parser;

namespace RecipeJoe.Api.Import;

/// <summary>Pure HTML → schema.org/Recipe (JSON-LD) parser. First usable Recipe wins.</summary>
internal static partial class RecipeParser
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static Result<ParsedRecipe, ImportFailure> Parse(string html, Uri pageUrl)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(pageUrl);

        var document = new HtmlParser().ParseDocument(html);

        foreach (var script in document.QuerySelectorAll("script[type=\"application/ld+json\" i]"))
        {
            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(script.TextContent, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            using (json)
            {
                var recipe = ToRecipe(json.RootElement);
                if (recipe is not null)
                {
                    return Result<ParsedRecipe, ImportFailure>.Ok(recipe);
                }
            }
        }

        return Result<ParsedRecipe, ImportFailure>.Fail(ImportFailure.NoRecipe);
    }

    private static ParsedRecipe? ToRecipe(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object || !IsRecipe(node))
        {
            return null;
        }

        var title = Text(node, "name");
        var lines = ReadStrings(node, "recipeIngredient").Select(Normalise).Where(l => l.Length > 0).ToList();
        var steps = ReadSteps(node).ToList();

        if (title is null || (lines.Count == 0 && steps.Count == 0))
        {
            return null;
        }

        return new ParsedRecipe(
            title,
            Servings(node),
            Duration(node, "prepTime"),
            Duration(node, "cookTime"),
            Duration(node, "totalTime"),
            lines,
            steps
        );
    }

    private static bool IsRecipe(JsonElement node) =>
        node.TryGetProperty("@type", out var type)
        && type.ValueKind == JsonValueKind.String
        && type.GetString() is "Recipe" or "https://schema.org/Recipe" or "http://schema.org/Recipe";

    private static string? Text(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = Normalise(value.GetString()!);
        return text.Length == 0 ? null : text;
    }

    private static IEnumerable<string> ReadStrings(JsonElement node, string property) =>
        node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)
            : [];

    private static IEnumerable<string> ReadSteps(JsonElement node)
    {
        if (!node.TryGetProperty("recipeInstructions", out var instructions) || instructions.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in instructions.EnumerateArray())
        {
            // HowToStep `name` is dropped; it is only a fallback when `text` is missing.
            var raw = item.ValueKind == JsonValueKind.String ? item.GetString() : Text(item, "text") ?? Text(item, "name");
            var step = Normalise(raw ?? string.Empty);
            if (step.Length > 0)
            {
                yield return step;
            }
        }
    }

    private static string? Servings(JsonElement node)
    {
        if (!node.TryGetProperty("recipeYield", out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : Normalise(text);
    }

    private static TimeSpan? Duration(JsonElement node, string property)
    {
        var text = Text(node, property);
        if (text is null)
        {
            return null;
        }

        try
        {
            var duration = XmlConvert.ToTimeSpan(text);
            return duration > TimeSpan.Zero ? duration : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Normalise(string text) =>
        Whitespace().Replace(WebUtility.HtmlDecode(text), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
