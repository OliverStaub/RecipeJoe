namespace RecipeJoe.Api.Recipes;

internal sealed record RecipeSummaryDto(int Id, string Title, string SourceUrl, bool HasImage);
