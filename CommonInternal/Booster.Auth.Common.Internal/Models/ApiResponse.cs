namespace Booster.Auth.Common.Internal.Models;


public sealed record ApiResponse<T>
{
    public T? Data { get; init; }

    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResponse<T> Ok(T? data) => new() { Data = data };
}

public sealed record ApiResponse
{
    public object? Data { get; init; }

    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResponse Ok(object? data) => new() { Data = data };
}
