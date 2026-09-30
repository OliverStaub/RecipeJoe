using RecipeJoe.Api.Images;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Import;

/// <summary>URL → Recipe draft: fetch → decode → parse → download image. Persists nothing; relative links resolve against the page after redirects, which is also the draft's Source. A failed image only leaves the draft without one.</summary>
internal sealed class Importer(FetchPolicy fetchPolicy, ImageDownloader images)
{
    public async Task<Result<RecipeDraft, ImportFailure>> ImportAsync(string url, CancellationToken cancellationToken)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var pageUrl)
            || (pageUrl.Scheme != Uri.UriSchemeHttp && pageUrl.Scheme != Uri.UriSchemeHttps)
        )
        {
            return Result<RecipeDraft, ImportFailure>.Fail(ImportFailure.InvalidUrl);
        }

        var page = await fetchPolicy.FetchPageAsync(pageUrl, cancellationToken);
        if (!page.IsSuccess)
        {
            return Result<RecipeDraft, ImportFailure>.Fail(page.Failure);
        }

        var parsed = RecipeParser.Parse(PageDecoder.Decode(page.Value), page.Value.Url);
        if (!parsed.IsSuccess)
        {
            return Result<RecipeDraft, ImportFailure>.Fail(parsed.Failure);
        }

        var recipe = parsed.Value;
        var image = await images.DownloadAsync(recipe.ImageUrl, cancellationToken);
        return Result<RecipeDraft, ImportFailure>.Ok(
            new RecipeDraft(
                recipe.Title,
                recipe.Servings,
                recipe.PrepTime,
                recipe.CookTime,
                recipe.TotalTime,
                recipe.IngredientLines,
                recipe.Steps,
                page.Value.Url,
                image
            )
        );
    }
}
