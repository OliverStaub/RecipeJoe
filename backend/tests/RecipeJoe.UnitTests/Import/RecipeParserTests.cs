using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

[TestClass]
public sealed class RecipeParserTests
{
    private static readonly Uri PageUrl = new("https://example.test/rezept");

    private static readonly string[] KartoffelsuppeLines =
    [
        "800 g Kartoffeln",
        "1 Zwiebel",
        "1 l Gemüsebrühe",
        "Salz und Pfeffer",
    ];

    private static readonly string[] KartoffelsuppeSteps =
    [
        "Kartoffeln und Zwiebel schälen und würfeln.",
        "In der Brühe 30 Minuten weich kochen.",
        "Pürieren und abschmecken.",
    ];

    private static string Page(string ldJson) =>
        $"<html><head><script type=\"application/ld+json\">{ldJson}</script></head><body></body></html>";

    private static string Fixture(string name) => File.ReadAllText(Path.Combine("fixtures", "recipes", name));

    [TestMethod]
    public void Parses_a_plain_recipe_with_HowToStep_instructions_and_ISO_durations()
    {
        var result = RecipeParser.Parse(Fixture("kartoffelsuppe.html"), PageUrl);

        Assert.IsTrue(result.IsSuccess);
        var recipe = result.Value;
        Assert.AreEqual("Kartoffelsuppe", recipe.Title);
        Assert.AreEqual("4 Portionen", recipe.Servings);
        Assert.AreEqual(TimeSpan.FromMinutes(15), recipe.PrepTime);
        Assert.AreEqual(TimeSpan.FromMinutes(30), recipe.CookTime);
        Assert.AreEqual(TimeSpan.FromMinutes(45), recipe.TotalTime);
        CollectionAssert.AreEqual(KartoffelsuppeLines, recipe.IngredientLines.ToArray());
        CollectionAssert.AreEqual(KartoffelsuppeSteps, recipe.Steps.ToArray());
    }

    [TestMethod]
    public void Parses_string_instructions_a_numeric_yield_and_hour_durations()
    {
        var recipe = RecipeParser.Parse(Fixture("apfelkuchen.html"), PageUrl).Value;

        Assert.AreEqual("12", recipe.Servings);
        Assert.AreEqual(TimeSpan.FromHours(1), recipe.CookTime);
        Assert.IsNull(recipe.TotalTime);
        Assert.HasCount(3, recipe.Steps);
        Assert.AreEqual("Äpfel schälen und in Scheiben schneiden.", recipe.Steps[0]);
    }

    [TestMethod]
    public void Decodes_html_entities_and_leaves_missing_optional_fields_null()
    {
        var recipe = RecipeParser.Parse(Fixture("linsensuppe.html"), PageUrl).Value;

        Assert.AreEqual("Linsensuppe & Würstchen", recipe.Title);
        Assert.IsNull(recipe.Servings);
        Assert.IsNull(recipe.PrepTime);
        Assert.IsNull(recipe.CookTime);
    }

    [TestMethod]
    public void A_malformed_duration_is_ignored_instead_of_failing_the_parse()
    {
        var html = Page(
            """{"@type":"Recipe","name":"X","prepTime":"bald","recipeIngredient":["a"],"recipeInstructions":["b"]}"""
        );

        var result = RecipeParser.Parse(html, PageUrl);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Value.PrepTime);
    }

    [TestMethod]
    public void Falls_back_to_the_step_name_when_a_HowToStep_has_no_text()
    {
        var html = Page(
            """{"@type":"Recipe","name":"X","recipeInstructions":[{"@type":"HowToStep","name":"Rühren"}]}"""
        );

        Assert.AreEqual("Rühren", RecipeParser.Parse(html, PageUrl).Value.Steps.Single());
    }

    [TestMethod]
    [DataRow("<html><body>Kein Rezept</body></html>", DisplayName = "no ld+json")]
    [DataRow("""<script type="application/ld+json">{"@type":"Article","name":"X"}</script>""", DisplayName = "no Recipe node")]
    [DataRow("""<script type="application/ld+json">{"@type":"Recipe","recipeIngredient":["a"]}</script>""", DisplayName = "no name")]
    [DataRow("""<script type="application/ld+json">{"@type":"Recipe","name":"X"}</script>""", DisplayName = "no lines and no steps")]
    [DataRow("""<script type="application/ld+json">{ kaputt</script>""", DisplayName = "broken json")]
    public void Fails_with_NoRecipe_when_there_is_no_usable_recipe(string html)
    {
        var result = RecipeParser.Parse(html, PageUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
    }
}
