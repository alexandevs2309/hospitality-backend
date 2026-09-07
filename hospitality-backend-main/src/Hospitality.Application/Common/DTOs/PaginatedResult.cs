namespace Hospitality.Application.Common.DTOs;

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new List<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    
    public PaginatedResult() { }
    
    public PaginatedResult(List<T> items, int count, int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = count;
        Items = items;
    }
    
    public static PaginatedResult<T> Create(IEnumerable<T> source, int pageNumber, int pageSize)
    {
        var count = source.Count();
        var items = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedResult<T>(items, count, pageNumber, pageSize);
    }
}

public class PaginatedQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "asc";
    public string? Search { get; set; }
    
    public bool IsValid()
    {
        return PageNumber > 0 && PageSize > 0 && PageSize <= 100;
    }
}