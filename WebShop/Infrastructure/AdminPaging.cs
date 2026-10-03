using System.Text;

namespace WebShop.Infrastructure;

public static class AdminPaging
{
    public static readonly int[] PageSizes = [20, 30, 50, 100];

    public const int DefaultPageSize = 20;

    public static int NormalizeSize(int size) =>
        PageSizes.Contains(size) ? size : DefaultPageSize;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static (int Page, int TotalPages) Clamp(int page, int totalCount, int pageSize)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(0, totalCount) / (double)pageSize));
        page = NormalizePage(page);
        if (page > totalPages)
            page = totalPages;
        return (page, totalPages);
    }

    public static string BuildUrl(string path, int page, int pageSize, params (string Key, string? Value)[] filters)
    {
        var sb = new StringBuilder();
        sb.Append(path);
        sb.Append("?PageNumber=").Append(page);
        sb.Append("&PageSize=").Append(NormalizeSize(pageSize));
        foreach (var (key, value) in filters)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            sb.Append('&').Append(Uri.EscapeDataString(key))
                .Append('=').Append(Uri.EscapeDataString(value.Trim()));
        }

        return sb.ToString();
    }
}

public sealed class AdminPagerModel
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public required Func<int, int, string> PageUrl { get; init; }
}

public abstract class PagedAdminPageModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = AdminPaging.DefaultPageSize;

    public int TotalCount { get; protected set; }

    public int TotalPages { get; protected set; }

    protected void ApplyPagingTotals(int totalCount)
    {
        TotalCount = totalCount;
        PageSize = AdminPaging.NormalizeSize(PageSize);
        (PageNumber, TotalPages) = AdminPaging.Clamp(PageNumber, TotalCount, PageSize);
    }

    public AdminPagerModel Pager(Func<int, int, string> pageUrl) => new()
    {
        PageNumber = PageNumber,
        PageSize = PageSize,
        TotalCount = TotalCount,
        TotalPages = TotalPages,
        PageUrl = pageUrl
    };
}
