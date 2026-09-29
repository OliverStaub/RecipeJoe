using RecipeJoe.Api.Images;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Import;

/// <summary>Fetch → parse → download image → save. Nothing is persisted unless the page yields a Recipe; a failed image only leaves the Recipe without one.</summary>
internal sealed class Importer(IPageFetcher fetcher, ImageDownloader images, RecipeJoeDbContext db, TimeProvider timeProvider)
{
    public async Task<Result<Recipe, ImportFailure>> ImportAsync(string url, CancellationToken cancellationToken)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var pageUrl)
            || (pageUrl.Scheme != Uri.UriSchemeHttp && pageUrl.Scheme != Uri.UriSchemeHttps)
        )
        {
            return Result<Recipe, ImportFailure>.Fail(ImportFailure.InvalidUrl);
        }

        var page = await fetcher.FetchAsync(pageUrl, cancellationToken);
        if (!page.IsSuccess)
        {
            return Result<Recipe, ImportFailure>.Fail(page.Failure);
        }

        var parsed = RecipeParser.Parse(PageDecoder.Decode(page.Value), pageUrl);
        if (!parsed.IsSuccess)
        {
            return Result<Recipe, ImportFailure>.Fail(parsed.Failure);
        }

        var recipe = ToEntity(parsed.Value, pageUrl);
        recipe.Image = await images.DownloadAsync(parsed.Value.ImageUrl, cancellationToken);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(cancellationToken);

        return Result<Recipe, ImportFailure>.Ok(recipe);
    }

    private Recipe ToEntity(ParsedRecipe parsed, Uri pageUrl)
    {
        var recipe = new Recipe
        {
            Title = parsed.Title,
            Servings = parsed.Servings,
            PrepTime = parsed.PrepTime,
            CookTime = parsed.CookTime,
            TotalTime = parsed.TotalTime,
            SourceUrl = pageUrl.AbsoluteUri,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        recipe.IngredientLines.AddRange(parsed.IngredientLines.Select((text, i) => new IngredientLine { Position = i, Text = text }));
        recipe.Steps.AddRange(parsed.Steps.Select((text, i) => new Step { Position = i, Text = text }));
        return recipe;
    }
}
