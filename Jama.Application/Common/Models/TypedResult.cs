namespace Jama.Application.Common.Models;

public class TypedResult<T>
{
    public bool Succeeded { get; init; }
    public T? Data { get; init; }
    public string[] Errors { get; init; } = [];

    public static TypedResult<T> Success(T data) =>
        new() { Succeeded = true, Data = data };

    public static TypedResult<T> Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };

    public static TypedResult<T> Failure(IEnumerable<string> errors) =>
        new() { Succeeded = false, Errors = errors.ToArray() };

    public static TypedResult<T> BadRequest() =>
        new() { Succeeded = false, Errors = ["Bad request."] };
}
