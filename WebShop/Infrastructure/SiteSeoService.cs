using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Sitewide crawl/index policy stored in SystemSettings (Admin → Cấu hình).
/// Config <c>Seo:AllowIndexing</c> is only the seed default when the DB row is missing.
/// </summary>
public sealed class SiteSeoService(
    AppDbContext db,
    IOptions<SeoOptions> options,
    IMemoryCache cache)
{
    public const string AllowIndexingKey = "Seo.AllowIndexing";
    private const string CacheKey = "site-seo:allow-indexing";

    public async Task EnsureSeedAsync(CancellationToken ct = default)
    {
        await UpsertIfMissingAsync(
            AllowIndexingKey,
            options.Value.AllowIndexing ? "1" : "0",
            "Cho phép Google / công cụ tìm kiếm index site (1=bật, 0=tắt).",
            ct);

        await UpsertIfMissingAsync("Email", "", "Email liên hệ (Schema Organization).", ct);
        await UpsertIfMissingAsync("OpeningHours", "Mo-Su 08:00-17:00", "Giờ mở cửa Schema (vd. Mo-Su 08:00-17:00).", ct);
        await UpsertIfMissingAsync("OrgType", "Store", "Schema @type: Organization | Store | LocalBusiness.", ct);
        await UpsertIfMissingAsync("YoutubeUrl", "", "Link YouTube (sameAs Schema).", ct);
    }

    private async Task UpsertIfMissingAsync(string key, string value, string description, CancellationToken ct)
    {
        if (await db.SystemSettings.AnyAsync(item => item.Key == key, ct))
            return;

        db.SystemSettings.Add(new SystemSetting
        {
            Key = key,
            Value = value,
            Description = description
        });
        await db.SaveChangesAsync(ct);
        if (key == AllowIndexingKey)
            cache.Remove(CacheKey);
    }

    public async Task<bool> GetAllowIndexingAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out bool cached))
            return cached;

        var raw = await db.SystemSettings.AsNoTracking()
            .Where(item => item.Key == AllowIndexingKey)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(ct);

        var allow = Parse(raw) ?? options.Value.AllowIndexing;
        cache.Set(CacheKey, allow, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
        });
        return allow;
    }

    public async Task SetAllowIndexingAsync(bool allow, CancellationToken ct = default)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == AllowIndexingKey, ct);
        if (setting is null)
        {
            setting = new SystemSetting
            {
                Key = AllowIndexingKey,
                Description = "Cho phép Google / công cụ tìm kiếm index site (1=bật, 0=tắt)."
            };
            db.SystemSettings.Add(setting);
        }

        setting.Value = allow ? "1" : "0";
        setting.Description ??= "Cho phép Google / công cụ tìm kiếm index site (1=bật, 0=tắt).";
        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);
    }

    private static bool? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return raw.Trim() switch
        {
            "1" or "true" or "True" or "yes" or "on" => true,
            "0" or "false" or "False" or "no" or "off" => false,
            _ => null
        };
    }
}
