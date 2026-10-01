using System.Text.Json;
using RecipeJoe.Api.Video;

namespace RecipeJoe.Sweep;

/// <summary>A recorded video and how many Recipes a good extraction finds in it (0 = NoRecipe).</summary>
internal sealed record GoldenCase(string VideoId, int ExpectedRecipes);

/// <summary>The golden cases, shared by `just golden` and the sweep. Recordings live in the golden test project and are copied next to this tool's binaries.</summary>
internal static class GoldenCases
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<GoldenCase> All { get; } =
    [
        new("i84Sc5uvQa8", 1), // EN, one pasta dish; description states "serves 2-3"
        new("6wR2T-PexT4", 5), // long DE, noisy start (ads, Russian), chapters "Rezept 1" to "Rezept 5"
        new("6tMZNYQkycI", 0), // transcript is only "[Music]"; the ten recipes exist only as on-screen text
    ];

    /// <summary>Reads a recording; a missing one is an error here (run `just golden` once to record it from YouTube).</summary>
    public static async Task<VideoText> LoadAsync(string videoId, CancellationToken cancellationToken)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Recordings", $"{videoId}.json");
        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"No recording for {videoId}. Run `just golden` once to record it.", file);
        }

        return JsonSerializer.Deserialize<VideoText>(await File.ReadAllTextAsync(file, cancellationToken), Json)!;
    }
}
