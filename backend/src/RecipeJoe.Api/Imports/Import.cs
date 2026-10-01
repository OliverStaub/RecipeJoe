using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Imports;

/// <summary>A running or Failed Import. Pending while <see cref="Stage"/> is set; Failed once <see cref="Failure"/> is. No Completed state: a successful Import simply disappears.</summary>
internal sealed record Import(Guid Id, Uri Url, ImportKind Kind, ImportStage? Stage, ImportFailure? Failure, DateTimeOffset StartedAt)
{
    public bool IsFailed => Failure is not null;
}
