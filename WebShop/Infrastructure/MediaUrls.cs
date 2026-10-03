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
    public static string? SrcSetUpTo(string? url, MediaSize maxSize)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !url.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = new List<string>();
        if (maxSize >= MediaSize.Icon)
            parts.Add($"{For(url, MediaSize.Icon)} {IconW}w");
        if (maxSize >= MediaSize.Thumb)
            parts.Add($"{For(url, MediaSize.Thumb)} {ThumbW}w");
        if (maxSize >= MediaSize.Medium)
            parts.Add($"{For(url, MediaSize.Medium)} {MediumW}w");
        if (maxSize >= MediaSize.Large)
            parts.Add($"{For(url, MediaSize.Large)} {LargeW}w");

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
