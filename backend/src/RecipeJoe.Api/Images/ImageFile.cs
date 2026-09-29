namespace RecipeJoe.Api.Images;

/// <summary>A Recipe's image as served: validated bytes and the type they were detected as.</summary>
internal sealed record ImageFile(string ContentType, byte[] Bytes);
