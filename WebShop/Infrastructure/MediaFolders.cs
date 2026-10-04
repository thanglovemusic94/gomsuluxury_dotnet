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
    public const int MaxDepth = 4; // 0-based: root + 4 levels max segments
    public const int MaxPathLength = 120;

    public static IReadOnlyList<(string Key, string Label)> Items { get; } =
    [
        (Products, "Sản phẩm"),
        (News, "Tin tức"),
        (Slider, "Slider"),
        (Pages, "Trang tĩnh"),
        (Other, "Khác")
    ];

    public static bool IsBuiltIn(string? folder)
    {
        var key = Normalize(folder);
        return Items.Any(item => item.Key == key);
    }

    /// <summary>Slug một segment thư mục (a-z0-9-). Rỗng nếu không hợp lệ.</summary>
    public static string ToSlug(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var slug = TextHelper.Slug(name.Trim(), "x");
        // TextHelper fallback "x" khi input không còn ký tự hợp lệ
        if (slug == "x" && name.Trim().IndexOf('x') < 0 && name.Trim().IndexOf('X') < 0)
            return string.Empty;
        if (slug.Length > 40)
            slug = slug[..40].Trim('-');
        return slug.Trim('-');
    }

    /// <summary>
    /// Chuẩn hóa key thư mục (có thể lồng: san-pham/chien-dich).
    /// </summary>
    public static string Normalize(string? folder)
    {
        folder = (folder ?? string.Empty).Trim().Trim('/').ToLowerInvariant().Replace('\\', '/');
        if (string.IsNullOrEmpty(folder) || folder is "all" or "tat-ca")
            return All;

        var segments = folder.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                // already slug-like segments stay; title names get slugged
                var s = part.Trim().ToLowerInvariant();
                if (RegexIsSlug(s))
                    return s.Length > 40 ? s[..40].Trim('-') : s;
                return ToSlug(part);
            })
            .Where(s => !string.IsNullOrWhiteSpace(s) && s is not ("all" or "tat-ca"))
            .Take(MaxDepth)
            .ToArray();

        if (segments.Length == 0)
            return All;

        var key = string.Join('/', segments);
        return key.Length > MaxPathLength ? key[..MaxPathLength].Trim('/') : key;
    }

    private static bool RegexIsSlug(string s) =>
        s.Length > 0 && s.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    public static string ParentKey(string? folder)
    {
        var key = Normalize(folder);
        if (key == All)
            return All;
        var i = key.LastIndexOf('/');
        return i < 0 ? All : key[..i];
    }

    /// <summary>0 = root folder; -1 = Tất cả.</summary>
    public static int Depth(string? folder)
    {
        var key = Normalize(folder);
        if (key == All)
            return -1;
        return key.Count(c => c == '/');
    }

    public static string LeafKey(string? folder)
    {
        var key = Normalize(folder);
        if (key == All)
            return All;
        var i = key.LastIndexOf('/');
        return i < 0 ? key : key[(i + 1)..];
    }

    public static string[] PathSegments(string? folder)
    {
        var key = Normalize(folder);
        return key == All ? [] : key.Split('/');
    }

    public static string RootKey(string? folder)
    {
        var key = Normalize(folder);
        if (key == All)
            return All;
        var i = key.IndexOf('/');
        return i < 0 ? key : key[..i];
    }

    public static string SegmentLabel(string? segment)
    {
        var key = (segment ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key))
            return "Tất cả";
        var builtIn = Items.FirstOrDefault(item => item.Key == key);
        if (!string.IsNullOrEmpty(builtIn.Key))
            return builtIn.Label;
        return string.Join(' ', key.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    /// <summary>Nhãn lá (sidebar). Với path đầy đủ dùng <see cref="Label"/>.</summary>
    public static string LeafLabel(string? folder) => SegmentLabel(LeafKey(folder));

    /// <summary>Nhãn đường dẫn: «Sản phẩm / Chiến dịch».</summary>
    public static string Label(string? folder)
    {
        var key = Normalize(folder);
        if (key == All)
            return "Tất cả";
        return string.Join(" / ", PathSegments(key).Select(SegmentLabel));
    }

    public static int RootSortOrder(string? folder)
    {
        var root = RootKey(folder);
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == root)
                return i;
        }
        return 1000;
    }
}

/// <summary>Mục thư mục media (có thể lồng).</summary>
public sealed record MediaFolderEntry(string Key, string Label, int Depth)
{
    public string IndentedLabel =>
        Depth <= 0 ? Label : new string('\u00A0', Depth * 2) + "└ " + Label;
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
