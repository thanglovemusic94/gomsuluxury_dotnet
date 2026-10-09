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
    /// <summary>Descriptor widths — synced from Admin Media settings.</summary>
    public static int IconW { get; private set; } = 96;
    public static int ThumbW { get; private set; } = 400;
    public static int MediumW { get; private set; } = 800;
    public static int LargeW { get; private set; } = 1200;
    /// <summary>OTF retina widths (ImageSharp.Web from originals).</summary>
    public const int HeroW = 1600;
    public const int Hero2xW = 1920;

    public static void SyncDescriptors(int icon, int thumb, int medium, int large)
    {
        IconW = icon;
        ThumbW = thumb;
        MediumW = medium;
        LargeW = large;
    }

    /// <summary>Home stage: slider fills the column beside the category rail (~340px).</summary>
    public const string SizesHomeSlider = "(max-width: 991px) 100vw, min(1480px, calc(100vw - 360px))";
    public const string SizesFullSlider = "(max-width: 991px) 100vw, min(1200px, 100vw)";
    public const string SizesProductGallery = "(max-width: 767px) 100vw, (max-width: 991px) 50vw, min(720px, 48vw)";
    /// <summary>Grid card ~220–280px; mobile 50vw — cần Medium 800 cho retina.</summary>
    public const string SizesProductCard = "(max-width: 575px) 50vw, 280px";
    /// <summary>Blog list / home swipe card.</summary>
    public const string SizesBlogCard = "(max-width: 767px) 85vw, 400px";
    public const string SizesPostCover = "(max-width: 767px) 100vw, 720px";

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
    /// Home/full slider: Medium → Large → OTF WebP 1600/1920 (retina sắc nét).
    /// Pass <paramref name="originalUrl"/> from <see cref="MediaOriginalLookup"/> when possible.
    /// </summary>
    public static string? SrcSetHero(string? url, string? originalUrl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var parts = new List<string>();
        var medium = For(url, MediaSize.Medium);
        var large = For(url, MediaSize.Large);

        if (!string.IsNullOrWhiteSpace(medium)
            && medium.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{medium} {MediumW}w");

        if (!string.IsNullOrWhiteSpace(large)
            && large.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{large} {LargeW}w");

        AppendHeroOtf(parts, url, originalUrl);
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>
    /// Srcset chi tiết SP: Medium → Large → OTF 1600/1920 → Original (nếu file còn).
    /// </summary>
    public static string? SrcSetDetail(string? url, string? originalUrl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var parts = new List<string>();
        var medium = For(url, MediaSize.Medium);
        var large = For(url, MediaSize.Large);

        if (!string.IsNullOrWhiteSpace(medium)
            && medium.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{medium} {MediumW}w");

        if (!string.IsNullOrWhiteSpace(large)
            && large.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{large} {LargeW}w");

        AppendHeroOtf(parts, url, originalUrl);

        var original = !string.IsNullOrWhiteSpace(originalUrl)
            ? originalUrl
            : For(url, MediaSize.Original);
        if (!string.IsNullOrWhiteSpace(original)
            && !string.Equals(original, large, StringComparison.OrdinalIgnoreCase)
            && !original.Contains('?', StringComparison.Ordinal)
            && MediaWebRoot.FileExistsForUrl(original))
            parts.Add($"{original} 2400w");

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Default &lt;img src&gt; for hero: Large preset (ổn định SEO), srcset lo retina.</summary>
    public static string HeroSrc(string? url)
    {
        var large = For(url, MediaSize.Large);
        if (!string.IsNullOrWhiteSpace(large))
            return large;
        return For(url, MediaSize.Medium);
    }

    /// <summary>Gallery main: Large preset (nhẹ hơn Original); srcset có OTF + Original.</summary>
    public static string GallerySrc(string? url)
    {
        var large = For(url, MediaSize.Large);
        if (!string.IsNullOrWhiteSpace(large)
            && large.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            return large;

        var full = For(url, MediaSize.Original);
        return !string.IsNullOrWhiteSpace(full) ? full : (url ?? string.Empty);
    }

    /// <summary>Product card: Medium (Max) — nét hơn Thumb 400 trên retina; CSS object-fit:cover.</summary>
    public static string CardSrc(string? url)
    {
        var medium = For(url, MediaSize.Medium);
        if (!string.IsNullOrWhiteSpace(medium))
            return medium;
        return For(url, MediaSize.Thumb);
    }

    /// <summary>Card srcset: Thumb → Medium (retina ~280×2).</summary>
    public static string? SrcSetCard(string? url) =>
        SrcSetBetween(url, MediaSize.Thumb, MediaSize.Medium);

    /// <summary>Blog card: Medium → Large (bỏ Thumb crop vuông cho khung 16/10).</summary>
    public static string? SrcSetBlogCard(string? url) =>
        SrcSetBetween(url, MediaSize.Medium, MediaSize.Large);

    /// <summary>Post cover: Medium → Large (+ OTF khi có original).</summary>
    public static string? SrcSetPostCover(string? url, string? originalUrl = null)
    {
        var parts = new List<string>();
        var medium = For(url, MediaSize.Medium);
        var large = For(url, MediaSize.Large);

        if (!string.IsNullOrWhiteSpace(medium)
            && medium.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{medium} {MediumW}w");

        if (!string.IsNullOrWhiteSpace(large)
            && large.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            parts.Add($"{large} {LargeW}w");

        AppendHeroOtf(parts, url, originalUrl);
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static void AppendHeroOtf(List<string> parts, string url, string? originalUrl)
    {
        string? w1600;
        string? w1920;
        if (!string.IsNullOrWhiteSpace(originalUrl)
            && MediaWebRoot.FileExistsForUrl(originalUrl))
        {
            w1600 = MediaDynamicUrls.HeroFromOriginal(originalUrl, HeroW);
            w1920 = MediaDynamicUrls.HeroFromOriginal(originalUrl, Hero2xW);
        }
        else
        {
            // Fallback: guessed originals path — only if file exists (avoid 404 srcset).
            var guessed = MediaDynamicUrls.ToOriginalPath(url);
            if (string.IsNullOrWhiteSpace(guessed) || !MediaWebRoot.FileExistsForUrl(guessed))
                return;
            w1600 = MediaDynamicUrls.HeroFromOriginal(guessed, HeroW);
            w1920 = MediaDynamicUrls.HeroFromOriginal(guessed, Hero2xW);
        }

        if (!string.IsNullOrWhiteSpace(w1600)
            && w1600.Contains('?', StringComparison.Ordinal))
            parts.Add($"{w1600} {HeroW}w");

        if (!string.IsNullOrWhiteSpace(w1920)
            && w1920.Contains('?', StringComparison.Ordinal)
            && !string.Equals(w1920, w1600, StringComparison.OrdinalIgnoreCase))
            parts.Add($"{w1920} {Hero2xW}w");
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
