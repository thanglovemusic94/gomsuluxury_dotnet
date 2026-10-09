using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebShop.Data;

namespace WebShop.Infrastructure;

/// <summary>
/// Maps optimized variant URLs → live OriginalUrl (DB), so ImageSharp.Web OTF works
/// even when regenerate changed stamps / folder layout.
/// </summary>
public sealed class MediaOriginalLookup(
    IServiceScopeFactory scopes,
    IMemoryCache cache)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(45);

    public string? Resolve(string? variantUrl)
    {
        if (string.IsNullOrWhiteSpace(variantUrl))
            return null;

        var norm = Normalize(variantUrl);
        if (string.IsNullOrWhiteSpace(norm))
            return null;

        var cacheKey = "media-orig:" + norm;
        if (cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        string? resolved = null;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.MediaAssets.AsNoTracking()
                .Where(item =>
                    item.OriginalUrl == norm
                    || item.MediumUrl == norm
                    || item.LargeUrl == norm
                    || item.ThumbUrl == norm)
                .Select(item => new { item.OriginalUrl })
                .FirstOrDefault();

            if (row is null && norm.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase))
            {
                // Same asset, different size folder in URL.
                foreach (var alt in AltSizeUrls(norm))
                {
                    row = db.MediaAssets.AsNoTracking()
                        .Where(item =>
                            item.MediumUrl == alt
                            || item.LargeUrl == alt
                            || item.ThumbUrl == alt)
                        .Select(item => new { item.OriginalUrl })
                        .FirstOrDefault();
                    if (row is not null)
                        break;
                }
            }

            if (row is not null
                && !string.IsNullOrWhiteSpace(row.OriginalUrl)
                && MediaWebRoot.FileExistsForUrl(row.OriginalUrl))
            {
                resolved = Normalize(row.OriginalUrl);
            }
        }
        catch
        {
            resolved = null;
        }

        cache.Set(cacheKey, resolved, CacheFor);
        return resolved;
    }

    private static string Normalize(string url)
    {
        var q = url.IndexOf('?', StringComparison.Ordinal);
        var path = (q >= 0 ? url[..q] : url).Trim();
        if (path.StartsWith("~/", StringComparison.Ordinal))
            path = path[1..];
        return path;
    }

    private static IEnumerable<string> AltSizeUrls(string optimizedUrl)
    {
        foreach (var size in new[] { "medium", "large", "thumb", "icon" })
        {
            yield return System.Text.RegularExpressions.Regex.Replace(
                optimizedUrl,
                @"/uploads/optimized/(icon|thumb|medium|large)/",
                $"/uploads/optimized/{size}/",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
