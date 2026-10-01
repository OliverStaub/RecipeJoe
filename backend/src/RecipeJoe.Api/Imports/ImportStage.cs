namespace RecipeJoe.Api.Imports;

/// <summary>Where a Pending Import is in its run. UI wording is per Import kind.</summary>
internal enum ImportStage
{
    Fetching,
    Extracting,
    Saving,
}
