namespace ApprovalDemo.Api.Models;

public sealed class Task8ReportResponse
{
    public int ReportId { get; init; }
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } = Array.Empty<IReadOnlyDictionary<string, string>>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string? DataSourceNote { get; init; }
}
