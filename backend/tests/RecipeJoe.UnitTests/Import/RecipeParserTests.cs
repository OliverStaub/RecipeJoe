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
    public void Keeps_newlines_inside_a_step()
    {
        var html = Page("""{"@type":"Recipe","name":"X","recipeInstructions":["Erst rühren.\n\n\n\nDann backen."]}""");

        Assert.AreEqual("Erst rühren.\n\nDann backen.", RecipeParser.Parse(html, PageUrl).Value.Steps.Single());
    }

    [TestMethod]
    public void An_absurdly_large_worded_duration_is_ignored()
    {
        var html = Page("""{"@type":"Recipe","name":"X","prepTime":"99999999999999 Std","recipeIngredient":["a"]}""");

        Assert.IsNull(RecipeParser.Parse(html, PageUrl).Value.PrepTime);
    }

    [TestMethod]
    public void A_self_referencing_step_list_terminates()
    {
        var html = Page(
            """[{"@type":"Recipe","name":"X","recipeIngredient":["a"],"recipeInstructions":[{"@id":"#a"}]},{"@id":"#a","itemListElement":[{"@id":"#a"}]}]"""
        );

        Assert.IsTrue(RecipeParser.Parse(html, PageUrl).IsSuccess);
    }

    [TestMethod]
    public void Falls_back_to_the_step_name_when_the_text_is_empty()
    {
        var html = Page("""{"@type":"Recipe","name":"X","recipeInstructions":[{"@type":"HowToStep","name":"Rühren","text":""}]}""");

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

    private static Uri? ImageOf(string imageJson, string id = "") =>
        RecipeParser
            .Parse(
                Page(
                    $$"""
                    {"@context":"https://schema.org","@graph":[{{id}}{"@type":"Recipe","name":"X","recipeIngredient":["a"],"image":{{imageJson}}}]}
                    """
                ),
                PageUrl
            )
            .Value.ImageUrl;

    [TestMethod]
    [DataRow("\"https://cdn.test/a.jpg\"", "https://cdn.test/a.jpg")]
    [DataRow("\"/bilder/a.jpg\"", "https://example.test/bilder/a.jpg")]
    [DataRow("\"a.jpg\"", "https://example.test/a.jpg")]
    [DataRow("\"//cdn.test/a.jpg\"", "https://cdn.test/a.jpg")]
    [DataRow("[\"https://cdn.test/1.jpg\",\"https://cdn.test/2.jpg\"]", "https://cdn.test/1.jpg")]
    [DataRow("{\"@type\":\"ImageObject\",\"url\":\"/u.jpg\",\"contentUrl\":\"/c.jpg\"}", "https://example.test/u.jpg")]
    [DataRow("{\"@type\":\"ImageObject\",\"contentUrl\":\"/c.jpg\"}", "https://example.test/c.jpg")]
    [DataRow("[{\"@type\":\"ImageObject\",\"url\":\"/u.jpg\"},\"/2.jpg\"]", "https://example.test/u.jpg")]
    public void Resolves_the_first_image_candidate_against_the_page_url(string imageJson, string expected)
    {
        Assert.AreEqual(expected, ImageOf(imageJson)?.AbsoluteUri);
    }

    [TestMethod]
    public void Resolves_an_image_given_as_an_id_reference()
    {
        var image = ImageOf("{\"@id\":\"#img\"}", "{\"@type\":\"ImageObject\",\"@id\":\"#img\",\"url\":\"/ref.jpg\"},");

        Assert.AreEqual("https://example.test/ref.jpg", image?.AbsoluteUri);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("\"\"")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("42")]
    [DataRow("\"ftp://cdn.test/a.jpg\"")]
    [DataRow("\"data:image/png;base64,AAAA\"")]
    [DataRow("[\"\",\"https://cdn.test/2.jpg\"]")]
    public void Has_no_image_when_the_first_candidate_is_unusable(string imageJson)
    {
        Assert.IsNull(ImageOf(imageJson));
    }
}
