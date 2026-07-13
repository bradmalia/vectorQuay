using System;

namespace VectorQuay.Core.Primitives;

/// <summary>
/// Represents the result of an operation that may succeed or fail, replacing silent empty-list returns.
/// </summary>
public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<string>? Error { get; }

    private ServiceResult(T? value, IReadOnlyList<string>? error)
    {
        IsSuccess = error is null;
        Value = value;
        Error = error;
    }

    public static ServiceResult<T> Success(T value) => new(value, null);
    public static ServiceResult<T> Failure(IReadOnlyList<string> error) => new(default!, error);
    public static ServiceResult<T> Failure(string message) => new(default!, [message]);

    /// <summary>If this result represents a failure (Error is not null), returns the error list.
    /// Otherwise, returns an empty sequence so callers never see null on Error.</summary>
    public IReadOnlyList<string> Errors => Error ?? [];
}
