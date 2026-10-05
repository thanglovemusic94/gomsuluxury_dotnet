using Microsoft.AspNetCore.Http;

namespace WebShop.Infrastructure;

/// <summary>Absolute URLs for favicon / Open Graph share previews.</summary>
public static class SiteSocial
{
    /// <summary>Khuyến nghị Facebook/Zalo: tỷ lệ ~1.91:1.</summary>
    public const int OgImageWidth = 1200;
    public const int OgImageHeight = 630;

    public static string Absolute(HttpRequest request, string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
            return string.Empty;

        var value = pathOrUrl.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("//", StringComparison.Ordinal))
        {
            if (value.StartsWith("//", StringComparison.Ordinal))
                value = $"{request.Scheme}:{value}";

            // Crawler FB/Zalo cần https cho og:image khi site chạy HTTPS (sau reverse proxy).
            if (string.Equals(request.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                && value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                value = "https://" + value["http://".Length..];

            return value;
        }

        if (!value.StartsWith('/'))
            value = "/" + value;

        return $"{request.Scheme}://{request.Host.Value}{value}";
    }

    public static string PickImage(params string?[] candidates)
    {
        foreach (var item in candidates)
        {
            if (!string.IsNullOrWhiteSpace(item))
                return item.Trim();
        }

        return string.Empty;
    }
}
