using System.Globalization;

namespace WebShop.Infrastructure;

/// <summary>
/// Builds on-the-fly ImageSharp.Web URLs against original uploads.
/// Prefer MediaUrls preset paths for shop SEO surfaces; use this for flexible sizes.
/// </summary>
public static class MediaDynamicUrls
{
    /// <summary>
    /// Returns an on-the-fly URL. Non-original optimized URLs are mapped back to originals first.
    /// </summary>
    public static string For(
        string? url,
        int width,
        string format = "webp",
        int? quality = null,
        string resizeMode = "max")
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var original = ToOriginalPath(url);
        if (string.IsNullOrWhiteSpace(original))
            return url;

        width = Math.Clamp(width, MediaImageSharpSetup.MinDimension, MediaImageSharpSetup.MaxWidth);
        quality ??= MediaImageSharpSetup.DefaultQuality;
        quality = Math.Clamp(quality.Value, 30, 90);

        format = (format ?? "webp").Trim().TrimStart('.');
        if (string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase))
            format = "jpg";

        if (string.IsNullOrWhiteSpace(resizeMode))
            resizeMode = "max";

        var qs = QueryString.Create(new Dictionary<string, string?>
        {
            ["width"] = width.ToString(CultureInfo.InvariantCulture),
            ["rmode"] = resizeMode,
            ["format"] = format,
            ["quality"] = quality.Value.ToString(CultureInfo.InvariantCulture)
        });

        return original + qs.ToUriComponent();
    }

    public static string ToOriginalPath(string url)
    {
        if (url.Contains("/uploads/originals/", StringComparison.OrdinalIgnoreCase))
            return StripQuery(url);

        if (url.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            return StripQuery(MediaUrls.For(url, MediaSize.Original));

        return StripQuery(url);
    }

    private static string StripQuery(string url)
    {
        var q = url.IndexOf('?', StringComparison.Ordinal);
        return q >= 0 ? url[..q] : url;
    }
}
