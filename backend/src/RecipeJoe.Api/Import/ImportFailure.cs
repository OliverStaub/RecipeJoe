namespace RecipeJoe.Api.Import;

/// <summary>Why an Import failed. The API returns only the kind; the frontend words it.</summary>
internal enum ImportFailure
{
    InvalidUrl,
    Unreachable,
    Blocked,
    NotFound,
    BadResponse,
    ForbiddenAddress,
    NoRecipe,

    /// <summary>The video has no caption tracks to read a Recipe from.</summary>
    NoCaptions,

    /// <summary>The LLM provider couldn't be reached, timed out or rejected the request. Details only in logs.</summary>
    LlmUnavailable,

    /// <summary>The LLM answered, but nothing usable came out of it.</summary>
    LlmBadOutput,

    /// <summary>An Import produced Recipe Drafts but none of them could be saved.</summary>
    SaveFailed,
}
