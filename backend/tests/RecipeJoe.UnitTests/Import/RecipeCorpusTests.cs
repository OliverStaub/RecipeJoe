using System.Text.Json;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

/// <summary>Runs the parser over every page in /fixtures/recipes and compares it with CorpusExpectations.json.</summary>
[TestClass]
public sealed class RecipeCorpusTests
{
    private static readonly string RecipesDir = Path.Combine(AppContext.BaseDirectory, "fixtures", "recipes");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, ExpectedRecipe> Expectations = JsonSerializer.Deserialize<Dictionary<string, ExpectedRecipe>>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Import", "CorpusExpectations.json")),
        JsonOptions
    )!;

    public static IEnumerable<string> RecipePages => Directory.GetFiles(RecipesDir, "*.html").Select(Path.GetFileName)!;

    public static IEnumerable<string> FailingPages =>
        Directory.GetFiles(Path.Combine(RecipesDir, "failing"), "*.html").Select(Path.GetFileName)!;

    [TestMethod]
    public void Every_corpus_page_has_an_expectation_and_the_corpus_has_thirty_pages()
    {
        CollectionAssert.AreEquivalent(Expectations.Keys.ToArray(), RecipePages.ToArray());
        Assert.HasCount(30, Expectations);
    }

    [TestMethod]
    [DynamicData(nameof(RecipePages))]
    public void Imports_the_page_as_expected(string file)
    {
        var result = RecipeParser.Parse(File.ReadAllText(Path.Combine(RecipesDir, file)), new Uri("https://example.test/" + file));

        Assert.IsTrue(result.IsSuccess, $"{file}: {(result.IsSuccess ? "" : result.Failure)}");
        var expected = Expectations[file];
        var recipe = result.Value;
        Assert.AreEqual(expected.Title, recipe.Title);
        Assert.AreEqual(expected.Servings, recipe.Servings);
        Assert.AreEqual(Minutes(expected.PrepMinutes), recipe.PrepTime);
        Assert.AreEqual(Minutes(expected.CookMinutes), recipe.CookTime);
        Assert.AreEqual(Minutes(expected.TotalMinutes), recipe.TotalTime);
        CollectionAssert.AreEqual(expected.IngredientLines, recipe.IngredientLines.ToArray());
        CollectionAssert.AreEqual(expected.Steps, recipe.Steps.ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(FailingPages))]
    public void Fails_with_NoRecipe_for_the_page(string file)
    {
        var result = RecipeParser.Parse(
            File.ReadAllText(Path.Combine(RecipesDir, "failing", file)),
            new Uri("https://example.test/" + file)
        );

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
    }

    private static TimeSpan? Minutes(int? minutes) => minutes is null ? null : TimeSpan.FromMinutes(minutes.Value);

    private sealed record ExpectedRecipe(
        string Title,
        string? Servings,
        int? PrepMinutes,
        int? CookMinutes,
        int? TotalMinutes,
        string[] IngredientLines,
        string[] Steps
    );
}
