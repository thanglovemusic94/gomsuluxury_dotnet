using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebShop.Data;

namespace WebShop.Infrastructure;

public sealed class HtmlBlockService(AppDbContext db, IMemoryCache cache, MediaTrashFilter trash)
{
    private static readonly Regex Shortcode = new(
        @"\{\{\s*block\s*:\s*([a-z0-9\-]+)\s*\}\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<string?> GetHtmlAsync(string key, CancellationToken ct = default)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return null;

        var cacheKey = $"html-block:{key}";
        if (cache.TryGetValue(cacheKey, out string? cached))
            return cached is null ? null : await trash.ScrubHtmlAsync(cached, ct);

        var html = await db.HtmlBlocks.AsNoTracking()
            .Where(block => block.Key == key && block.IsActive)
            .Select(block => block.Content)
            .FirstOrDefaultAsync(ct);

        if (html is not null)
            cache.Set(cacheKey, html, CacheTtl);

        return html is null ? null : await trash.ScrubHtmlAsync(html, ct);
    }

    public async Task<string> ExpandAsync(string? html, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        if (html.Contains("{{", StringComparison.Ordinal))
        {
            var matches = Shortcode.Matches(html);
            if (matches.Count > 0)
            {
                var result = html;
                foreach (Match match in matches)
                {
                    var key = match.Groups[1].Value;
                    var blockHtml = await GetHtmlAsync(key, ct) ?? string.Empty;
                    result = result.Replace(match.Value, blockHtml, StringComparison.OrdinalIgnoreCase);
                }

                html = result;
            }
        }

        return await trash.ScrubHtmlAsync(html, ct);
    }

    public void Invalidate(string? key = null)
    {
        if (!string.IsNullOrWhiteSpace(key))
            cache.Remove($"html-block:{NormalizeKey(key)}");
    }

    public static string NormalizeKey(string? key)
    {
        key = TextHelper.Slug(key, key ?? string.Empty);
        return key;
    }
}
