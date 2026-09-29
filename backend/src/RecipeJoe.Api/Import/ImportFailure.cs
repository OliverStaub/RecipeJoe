namespace RecipeJoe.Api.Import;

/// <summary>Why an Import failed. The API returns only the kind; the frontend words it.</summary>
public enum ImportFailure
{
    InvalidUrl,
    Unreachable,
    Blocked,
    NotFound,
    BadResponse,
    ForbiddenAddress,
    NoRecipe,
}
