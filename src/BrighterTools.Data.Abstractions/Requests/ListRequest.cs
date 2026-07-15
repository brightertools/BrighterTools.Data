namespace BrighterTools.Data.Abstractions.Requests;

public class ListRequest
{
    private int _page = 1;
    private int _pageSize = 10;

    public int Page
    {
        get => Math.Max(_page, 1);
        set => _page = Math.Max(value, 1);
    }

    public int PageSize
    {
        get => Math.Max(_pageSize, 1);
        set => _pageSize = Math.Max(value, 1);
    }

    public string SortBy { get; set; } = string.Empty;
    public bool SortByDescending { get; set; }
    public string SortDirection => SortByDescending ? "desc" : "asc";

    public string? SearchString { get; set; } = string.Empty;
    public bool GetTotalCount { get; set; }

    /// <summary>
    /// Dictionary of foreign key filters where Key = column name and Value = ID to filter by.
    /// </summary>
    public Dictionary<string, int>? ForeignKeyFilters { get; set; } = [];

    /// <summary>
    /// Enum filters where Key = column name and Value = Enum string value.
    /// </summary>
    public Dictionary<string, string>? EnumFilters { get; set; } = [];
}
