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
    private const int MaxNesting = 32;

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
                var nodes = new NodeIndex(json.RootElement);
                foreach (var candidate in nodes.Candidates())
                {
                    var recipe = ToRecipe(candidate, nodes);
                    if (recipe is not null)
                    {
                        return Result<ParsedRecipe, ImportFailure>.Ok(recipe);
                    }
                }
            }
        }

        return Result<ParsedRecipe, ImportFailure>.Fail(ImportFailure.NoRecipe);
    }

    private static ParsedRecipe? ToRecipe(JsonElement node, NodeIndex nodes)
    {
        var title = Text(node, "name");
        var lines = ReadLines(node).ToList();
        var steps = ReadSteps(node, nodes).ToList();

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

    /// <summary>The nodes of one JSON-LD block: root object / array / @graph, indexed by @id.</summary>
    private sealed class NodeIndex
    {
        private readonly List<JsonElement> _nodes = [];
        private readonly Dictionary<string, JsonElement> _byId = new(StringComparer.Ordinal);

        public NodeIndex(JsonElement root) => Collect(root);

        public JsonElement Resolve(JsonElement value) =>
            value.ValueKind == JsonValueKind.Object
            && value.EnumerateObject().Count() == 1
            && value.TryGetProperty("@id", out var id)
            && id.ValueKind == JsonValueKind.String
            && _byId.TryGetValue(id.GetString()!, out var target)
                ? target
                : value;

        /// <summary>Recipe nodes in document order, including a WebPage's mainEntity.</summary>
        public IEnumerable<JsonElement> Candidates()
        {
            foreach (var node in _nodes)
            {
                if (HasType(node, "Recipe"))
                {
                    yield return node;
                }
                else if (HasType(node, "WebPage") && node.TryGetProperty("mainEntity", out var main))
                {
                    var target = Resolve(main);
                    if (target.ValueKind == JsonValueKind.Object && HasType(target, "Recipe"))
                    {
                        yield return target;
                    }
                }
            }
        }

        private void Collect(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Collect(item);
                    }

                    break;
                case JsonValueKind.Object:
                    _nodes.Add(element);
                    if (element.TryGetProperty("@id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        _byId.TryAdd(id.GetString()!, element);
                    }

                    if (element.TryGetProperty("@graph", out var graph))
                    {
                        Collect(graph);
                    }

                    break;
            }
        }
    }

    private static bool HasType(JsonElement node, string type)
    {
        if (!node.TryGetProperty("@type", out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => IsType(value.GetString()!, type),
            JsonValueKind.Array => value.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String && IsType(e.GetString()!, type)),
            _ => false,
        };
    }

    private static bool IsType(string raw, string type) => SchemaOrgPrefix().Replace(raw.Trim(), "") == type;

    private static string? Text(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = Normalise(value.GetString()!);
        return text.Length == 0 ? null : text;
    }

    private static IEnumerable<string> ReadLines(JsonElement node)
    {
        var property = node.TryGetProperty("recipeIngredient", out var value) ? value
            : node.TryGetProperty("ingredients", out var legacy) ? legacy
            : default;

        var raw = property.ValueKind switch
        {
            JsonValueKind.Array => property.EnumerateArray().Select(Stringify),
            JsonValueKind.String => property.GetString()!.Split('\n'),
            _ => [],
        };

        return raw.OfType<string>().Select(l => Normalise(l)).Where(l => l.Length > 0);
    }

    private static string? Stringify(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        };

    private static IEnumerable<string> ReadSteps(JsonElement node, NodeIndex nodes)
    {
        if (!node.TryGetProperty("recipeInstructions", out var instructions))
        {
            return [];
        }

        // A single string is a blob of Steps, one per line; strings inside lists are one Step each.
        if (instructions.ValueKind == JsonValueKind.String)
        {
            return Normalise(instructions.GetString()!, keepNewlines: true)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return FlattenSteps(instructions, nodes, 0);
    }

    private static IEnumerable<string> FlattenSteps(JsonElement element, NodeIndex nodes, int depth)
    {
        // @id references can form cycles; real pages nest a handful of levels at most.
        if (depth > MaxNesting)
        {
            yield break;
        }

        element = nodes.Resolve(element);
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var step = Normalise(element.GetString()!, keepNewlines: true);
                if (step.Length > 0)
                {
                    yield return step;
                }

                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray().SelectMany(e => FlattenSteps(e, nodes, depth + 1)))
                {
                    yield return child;
                }

                break;
            case JsonValueKind.Object when element.TryGetProperty("itemListElement", out var items):
                // HowToSection / ItemList: flatten recursively.
                foreach (var child in FlattenSteps(items, nodes, depth + 1))
                {
                    yield return child;
                }

                break;
            case JsonValueKind.Object:
                // HowToStep `name` is dropped; it is only a fallback when `text` is missing.
                var text = Normalise(RawText(element, "text") ?? string.Empty, keepNewlines: true) is { Length: > 0 } own
                    ? own
                    : Normalise(RawText(element, "name") ?? string.Empty, keepNewlines: true);
                if (text.Length > 0)
                {
                    yield return text;
                }

                break;
        }
    }

    private static string? RawText(JsonElement node, string property) =>
        node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Servings(JsonElement node)
    {
        if (!node.TryGetProperty("recipeYield", out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.Array => value
                .EnumerateArray()
                .Select(e => Stringify(e) is { } s ? Normalise(s) : null)
                .OfType<string>()
                .OrderByDescending(s => s.Length)
                .FirstOrDefault(),
            JsonValueKind.Object => QuantitativeValue(value),
            _ => Stringify(value),
        };

        text = text is null ? null : Normalise(text);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? QuantitativeValue(JsonElement value)
    {
        if (!value.TryGetProperty("value", out var amount) || Stringify(amount) is not { } number)
        {
            return null;
        }

        return RawText(value, "unitText") is { } unit ? $"{number} {unit}" : number;
    }

    private static TimeSpan? Duration(JsonElement node, string property)
    {
        var text = Text(node, property);
        if (text is null)
        {
            return null;
        }

        var duration = ParseIso(text) ?? ParseIso(text.ToUpperInvariant()) ?? ParseWords(text);
        return duration > TimeSpan.Zero ? duration : null;
    }

    private static TimeSpan? ParseIso(string text)
    {
        try
        {
            return XmlConvert.ToTimeSpan(text);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    // "1 Std. 15 Min.", "1 hour 30 minutes": sums every number that is followed by a unit.
    private static TimeSpan? ParseWords(string text)
    {
        var total = TimeSpan.Zero;
        var found = false;
        foreach (Match match in DurationPart().Matches(text))
        {
            var amount = double.Parse(match.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            try
            {
                total += unit[0] is 'h' or 's' ? TimeSpan.FromHours(amount) : TimeSpan.FromMinutes(amount);
            }
            catch (Exception e) when (e is OverflowException or ArgumentException)
            {
                return null;
            }

            found = true;
        }

        return found ? total : null;
    }

    // Decode (twice if entities remain), <br> → newline, strip tags, collapse whitespace.
    // Steps keep their newlines (\r\n → \n, 3+ newlines → 2); everything else is a single line.
    private static string Normalise(string text, bool keepNewlines = false)
    {
        var decoded = WebUtility.HtmlDecode(text);
        if (Entity().IsMatch(decoded))
        {
            decoded = WebUtility.HtmlDecode(decoded);
        }

        decoded = LineBreak().Replace(decoded.Replace('\u00A0', ' ').Replace("\r\n", "\n", StringComparison.Ordinal), "\n");
        decoded = Tag().Replace(decoded, "");

        if (!keepNewlines)
        {
            return Whitespace().Replace(decoded, " ").Trim();
        }

        var lines = HorizontalWhitespace().Replace(decoded, " ").Split('\n').Select(l => l.Trim());
        return ExcessNewlines().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    [GeneratedRegex(@"^(https?://)?(www\.)?schema\.org/", RegexOptions.IgnoreCase)]
    private static partial Regex SchemaOrgPrefix();

    [GeneratedRegex(@"&(#\d+|#x[0-9a-f]+|[a-z][a-z0-9]*);", RegexOptions.IgnoreCase)]
    private static partial Regex Entity();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"</?[a-z][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Tag();

    [GeneratedRegex(@"(?<n>\d+(?:[.,]\d+)?)\s*(?<unit>h|hr|hrs|hour|hours|std|stunde|stunden|min|mins|minute|minutes|minuten)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPart();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlines();
}
