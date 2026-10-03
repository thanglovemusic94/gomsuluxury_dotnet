namespace WebShop.Infrastructure;

public static class MediaFolders
{
    public const string All = "";
    public const string Products = "san-pham";
    public const string News = "tin-tuc";
    public const string Slider = "slider";
    public const string Pages = "trang";
    public const string Other = "khac";

    public const int PageSize = 24;

    public static IReadOnlyList<(string Key, string Label)> Items { get; } =
    [
        (Products, "Sản phẩm"),
        (News, "Tin tức"),
        (Slider, "Slider"),
        (Pages, "Trang tĩnh"),
        (Other, "Khác")
    ];

    public static string Normalize(string? folder)
    {
        folder = (folder ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(folder) || folder is "all" or "tat-ca")
            return All;
        return Items.Any(item => item.Key == folder) ? folder : Other;
    }

    public static string Label(string? folder)
    {
        var key = Normalize(folder);
        if (key == All)
            return "Tất cả";
        return Items.FirstOrDefault(item => item.Key == key).Label ?? "Khác";
    }
}

public sealed class MediaQuery
{
    public string Type { get; init; } = "Images";
    public string Folder { get; init; } = MediaFolders.All;
    public string? Q { get; init; }
    public bool MissingAlt { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = MediaFolders.PageSize;
}

public sealed class MediaPageResult
{
    public IReadOnlyList<MediaItem> Items { get; init; } = [];
    public IReadOnlyDictionary<string, int> FolderCounts { get; init; } =
        new Dictionary<string, int>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
