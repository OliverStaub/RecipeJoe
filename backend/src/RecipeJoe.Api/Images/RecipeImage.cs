namespace RecipeJoe.Api.Images;

/// <summary>The downloaded image of a Recipe, stored as-is. Lives in its own table so it is never loaded with the Recipe.</summary>
internal sealed class RecipeImage
{
    public int RecipeId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Bytes { get; set; }
}
