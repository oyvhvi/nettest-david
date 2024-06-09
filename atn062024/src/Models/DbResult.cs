using System.Diagnostics.CodeAnalysis;

namespace atn062024.Models;

public enum FailureType
{
    NotFound,
    Conflict,
    BadRequest
}

public sealed class DbResult
{
    public Boolean Success { get; }
    public FailureType? FailureType { get; }

    private DbResult(Boolean success, FailureType? failureType)
    {
        Success = success;
        FailureType = failureType;
    }

    public static DbResult CreateSuccess() => new(true, null);
    public static DbResult CreateFailure(FailureType failureType) => new(false, failureType);
}

public sealed class DbResult<T> where T : notnull
{
    [MemberNotNullWhen(true, nameof(Value))]
    public Boolean Success { get; }

    public FailureType? FailureType { get; }

    public T? Value { get; }

    private DbResult(Boolean success, T? value, FailureType? failureType)
    {
        Success = success;
        FailureType = failureType;
        Value = value;
    }

    public static DbResult<T> CreateSuccess(T value) => new(true, value, null);
    public static DbResult<T> CreateFailure(FailureType failureType) => new(false, default, failureType);
}