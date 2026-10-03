using Microsoft.AspNetCore.Http;

namespace WebShop.Infrastructure;

/// <summary>Absolute URLs for favicon / Open Graph share previews.</summary>
public static class SiteSocial
{
    public static string Absolute(HttpRequest request, string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
            return string.Empty;

        var value = pathOrUrl.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("//", StringComparison.Ordinal))
            return value.StartsWith("//", StringComparison.Ordinal)
                ? $"{request.Scheme}:{value}"
                : value;

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
