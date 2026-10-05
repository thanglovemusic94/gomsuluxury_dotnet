namespace WebShop.Infrastructure;

public enum MediaSize
{
    Original,
    Icon,
    Thumb,
    Medium,
    Large
}

public static class MediaUrls
{
    /// <summary>Descriptor widths aligned with MediaOptions defaults.</summary>
    public const int IconW = 96;
    public const int ThumbW = 400;
    public const int MediumW = 800;
    public const int LargeW = 1200;

    public static string For(string? url, MediaSize size)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (!url.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            return url;

        var target = size switch
        {
            MediaSize.Icon => "icon",
            MediaSize.Thumb => "thumb",
            MediaSize.Large => "large",
            MediaSize.Original => null,
            _ => "medium"
        };

        if (target is null)
            return url.Replace("/uploads/optimized/icon/", "/uploads/originals/", StringComparison.OrdinalIgnoreCase)
                .Replace("/uploads/optimized/thumb/", "/uploads/originals/", StringComparison.OrdinalIgnoreCase)
                .Replace("/uploads/optimized/medium/", "/uploads/originals/", StringComparison.OrdinalIgnoreCase)
                .Replace("/uploads/optimized/large/", "/uploads/originals/", StringComparison.OrdinalIgnoreCase)
                .Replace(".webp", GuessOriginalExt(url), StringComparison.OrdinalIgnoreCase);

        return RegexReplaceSize(url, target);
    }

    public static string Display(string? url, MediaSize size) => For(url, size);

    /// <summary>
    /// Full srcset (thumb/medium/large). Null when URL is not an optimized upload.
    /// </summary>
    public static string? SrcSet(string? url) => SrcSetUpTo(url, MediaSize.Large);

    /// <summary>
    /// Srcset up to <paramref name="maxSize"/> (inclusive). Use Large for home slider (~1200px).
    /// </summary>
    public static string? SrcSetUpTo(string? url, MediaSize maxSize) =>
        SrcSetBetween(url, MediaSize.Icon, maxSize);

    /// <summary>
    /// Srcset from <paramref name="minSize"/>..max (inclusive). Banner/slider: Medium..Large
    /// (bỏ Thumb/Icon — hai size đó crop vuông).
    /// </summary>
    public static string? SrcSetBetween(string? url, MediaSize minSize, MediaSize maxSize)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !url.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            return null;

        if (minSize > maxSize)
            (minSize, maxSize) = (maxSize, minSize);

        var parts = new List<string>();
        void Add(MediaSize size, int width)
        {
            if (size >= minSize && size <= maxSize)
                parts.Add($"{For(url, size)} {width}w");
        }

        Add(MediaSize.Icon, IconW);
        Add(MediaSize.Thumb, ThumbW);
        Add(MediaSize.Medium, MediumW);
        Add(MediaSize.Large, LargeW);

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>
    /// Srcset chi tiết SP: Medium → Large → Original (bỏ Icon/Thumb để tránh trình duyệt chọn ảnh mờ).
    /// </summary>
    public static string? SrcSetDetail(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var parts = new List<string>();
        var medium = For(url, MediaSize.Medium);
        var large = For(url, MediaSize.Large);
        var original = For(url, MediaSize.Original);

        if (!string.IsNullOrWhiteSpace(medium)
            && medium.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{medium} {MediumW}w");

        if (!string.IsNullOrWhiteSpace(large)
            && large.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{large} {LargeW}w");

        if (!string.IsNullOrWhiteSpace(original)
            && !string.Equals(original, large, StringComparison.OrdinalIgnoreCase))
            parts.Add($"{original} 2400w");

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string RegexReplaceSize(string url, string target)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            url,
            @"/uploads/optimized/(icon|thumb|medium|large)/",
            $"/uploads/optimized/{target}/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string GuessOriginalExt(string optimizedUrl) =>
        optimizedUrl.Contains(".webp", StringComparison.OrdinalIgnoreCase) ? ".jpg" : Path.GetExtension(optimizedUrl);
}
