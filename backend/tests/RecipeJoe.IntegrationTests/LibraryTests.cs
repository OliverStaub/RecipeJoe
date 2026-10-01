using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeJoe.Api;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public sealed class LibraryTests
{
    private static ApiFactory _factory = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _factory = new ApiFactory();

    [ClassCleanup]
    public static void ClassCleanup() => _factory.Dispose();

    [TestCleanup]
    public async Task ResetDatabaseAsync() => await _factory.ResetDatabaseAsync();

    /// <summary>Saved oldest first, so the list shows them in reverse.</summary>
    private static async Task SeedAsync(params (string Title, string[] Lines)[] recipes)
    {
        using var scope = _factory.Services.CreateScope();
        var library = scope.ServiceProvider.GetRequiredService<Library>();
        foreach (var (title, lines) in recipes)
        {
            await library.SaveAsync(
                new RecipeDraft(title, null, null, null, null, lines, [], new Uri("http://fixtures.test/recipes/x.html"), null),
                CancellationToken.None
            );
        }
    }

    private static async Task<string[]> SearchAsync(string? q)
    {
        var url = q is null ? "/api/recipes" : $"/api/recipes?q={Uri.EscapeDataString(q)}";
        var list = await _factory.CreateClient().GetFromJsonAsync<JsonElement>(url);
        return [.. list.EnumerateArray().Select(r => r.GetProperty("title").GetString()!)];
    }

    private static async Task AssertTitlesAsync(string? q, params string[] expected) =>
        CollectionAssert.AreEqual(expected, await SearchAsync(q));

    private static Task SeedSoupsAsync() =>
        SeedAsync(
            ("Kartoffelsuppe", ["800 g Kartoffeln", "1 Zwiebel"]),
            ("Linsensuppe", ["250 g Tellerlinsen", "2 Karotten"]),
            ("Apfelkuchen", ["4 Äpfel", "200 g Mehl"])
        );

    [TestMethod]
    public async Task Lists_the_whole_library_newest_first_for_a_missing_or_blank_q()
    {
        await SeedSoupsAsync();

        await AssertTitlesAsync(null, "Apfelkuchen", "Linsensuppe", "Kartoffelsuppe");
        await AssertTitlesAsync("  ", "Apfelkuchen", "Linsensuppe", "Kartoffelsuppe");
    }

    [TestMethod]
    public async Task Summaries_carry_id_title_source_and_image_flag()
    {
        await SeedSoupsAsync();

        var list = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/recipes");

        var first = list[0];
        Assert.IsGreaterThan(0, first.GetProperty("id").GetInt32());
        Assert.AreEqual("http://fixtures.test/recipes/x.html", first.GetProperty("sourceUrl").GetString());
        Assert.IsFalse(first.GetProperty("hasImage").GetBoolean());
    }

    [TestMethod]
    public async Task A_freshly_saved_Recipe_is_new()
    {
        await SeedSoupsAsync();

        var list = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/recipes");

        Assert.IsTrue(list.EnumerateArray().All(r => r.GetProperty("isNew").GetBoolean()));
    }

    [TestMethod]
    public async Task A_Recipe_stops_being_new_once_it_is_fetched_by_id()
    {
        await SeedSoupsAsync();
        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<JsonElement>("/api/recipes");
        var id = list[0].GetProperty("id").GetInt32();

        await client.GetAsync($"/api/recipes/{id}");

        var after = await client.GetFromJsonAsync<JsonElement>("/api/recipes");
        var summary = after.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        Assert.IsFalse(summary.GetProperty("isNew").GetBoolean());
    }

    [TestMethod]
    public async Task A_second_fetch_leaves_an_already_seen_Recipes_SeenAt_unchanged()
    {
        await SeedSoupsAsync();
        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<JsonElement>("/api/recipes");
        var id = list[0].GetProperty("id").GetInt32();

        await client.GetAsync($"/api/recipes/{id}");
        var seenAtAfterFirstGet = await SeenAtAsync(id);

        await client.GetAsync($"/api/recipes/{id}");
        var seenAtAfterSecondGet = await SeenAtAsync(id);

        Assert.IsNotNull(seenAtAfterFirstGet);
        Assert.AreEqual(seenAtAfterFirstGet, seenAtAfterSecondGet);
    }

    private static async Task<DateTimeOffset?> SeenAtAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeJoeDbContext>();
        return await db.Recipes.AsNoTracking().Where(r => r.Id == id).Select(r => r.SeenAt).SingleAsync();
    }

    [TestMethod]
    public async Task A_backfilled_Recipe_is_not_new()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeJoeDbContext>();
        var recipe = new Recipe
        {
            Title = "Backfilled",
            SourceUrl = "http://fixtures.test/recipes/old.html",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
        };
        recipe.SeenAt = recipe.CreatedAt;
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var list = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/recipes");

        var summary = list.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == recipe.Id);
        Assert.IsFalse(summary.GetProperty("isNew").GetBoolean());
    }

    [TestMethod]
    public async Task Matches_the_title()
    {
        await SeedSoupsAsync();

        await AssertTitlesAsync("suppe", "Linsensuppe", "Kartoffelsuppe");
    }

    [TestMethod]
    public async Task Matches_an_ingredient_line()
    {
        await SeedSoupsAsync();

        await AssertTitlesAsync("zwiebel", "Kartoffelsuppe");
    }

    [TestMethod]
    public async Task Tokens_are_anded_across_title_and_lines()
    {
        await SeedSoupsAsync();

        await AssertTitlesAsync("suppe  zwiebel", "Kartoffelsuppe");
        await AssertTitlesAsync("linsen zwiebel");
    }

    [TestMethod]
    public async Task Matching_ignores_case()
    {
        await SeedSoupsAsync();

        await AssertTitlesAsync("LINSENSUPPE", "Linsensuppe");
    }

    [TestMethod]
    public async Task No_match_returns_an_empty_list()
    {
        await SeedSoupsAsync();

        await AssertTitlesAsync("schnitzel");
    }

    [TestMethod]
    [DataRow("%")]
    [DataRow("_")]
    [DataRow("\\")]
    public async Task Wildcard_characters_match_literally(string q)
    {
        await SeedAsync(
            ("Plain", ["flour"]),
            ("100% Saft", ["a_b"]),
            ("Back\\slash", ["x"])
        );

        var expected = q == "\\" ? "Back\\slash" : "100% Saft";
        await AssertTitlesAsync(q, expected);
    }
}
