namespace RecipeJoe.UnitTests.ImportsModule;

/// <summary>Records every reported value synchronously, unlike <see cref="Progress{T}"/> which posts to a captured context.</summary>
internal sealed class RecordingProgress<T> : IProgress<T>
{
    private readonly List<T> _reported = [];

    public IReadOnlyList<T> Reported => _reported;

    public void Report(T value) => _reported.Add(value);
}
