namespace Jama.Application.Common.Models;

public sealed record ApiResult<T>(bool Succeeded, T? Data, string[] Errors)
{
    public static ApiResult<T> Success(T data) => new(true, data, []);
    public static ApiResult<T> Failure(params string[] errors) => new(false, default, errors);
}

public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);
