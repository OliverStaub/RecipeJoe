namespace RecipeJoe.Api;

/// <summary>Success value or a failure kind, for operations whose failures are part of the domain.</summary>
internal readonly record struct Result<T, TFailure>
{
    private readonly T? _value;
    private readonly TFailure? _failure;

    private Result(T? value, TFailure? failure, bool isSuccess)
    {
        _value = value;
        _failure = failure;
        IsSuccess = isSuccess;
    }

    public bool IsSuccess { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Result is a failure.");

    public TFailure Failure => !IsSuccess ? _failure! : throw new InvalidOperationException("Result is a success.");

    public static Result<T, TFailure> Ok(T value) => new(value, default, true);

    public static Result<T, TFailure> Fail(TFailure failure) => new(default, failure, false);
}
