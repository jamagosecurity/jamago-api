namespace Jama.Application.Common;

public class PaginationQuery
{
    private const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize);
    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
}
