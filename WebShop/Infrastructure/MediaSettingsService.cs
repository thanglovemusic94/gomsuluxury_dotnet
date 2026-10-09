using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Media width/quality from SystemSettings (Admin → Cấu hình).
/// appsettings <c>Media:*</c> is the seed default when DB rows are missing.
/// </summary>
public sealed class MediaSettingsService(
    IServiceScopeFactory scopes,
    IOptions<MediaOptions> defaults,
    IWebHostEnvironment env)
{
    public const string IconWidthKey = "Media.IconWidth";
    public const string ThumbWidthKey = "Media.ThumbWidth";
    public const string MediumWidthKey = "Media.MediumWidth";
    public const string LargeWidthKey = "Media.LargeWidth";
    public const string WebpQualityKey = "Media.WebpQuality";
    public const string OtfQualityKey = "Media.OtfQuality";

    public static readonly HashSet<string> ManagedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        IconWidthKey,
        ThumbWidthKey,
        MediumWidthKey,
        LargeWidthKey,
        WebpQualityKey,
        OtfQualityKey
    };

    private readonly object _gate = new();
    private MediaTune _current = MediaTune.FromOptions(defaults.Value);

    /// <summary>Current widths / quality (thread-safe snapshot).</summary>
    public MediaTune Current
    {
        get { lock (_gate) return _current; }
    }

    public async Task EnsureSeedAsync(CancellationToken ct = default)
    {
        var d = defaults.Value;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await UpsertIfMissingAsync(db, IconWidthKey, d.IconWidth.ToString(), "Chiều rộng Icon (crop vuông).", ct);
        await UpsertIfMissingAsync(db, ThumbWidthKey, d.ThumbWidth.ToString(), "Chiều rộng Thumb (crop vuông).", ct);
        await UpsertIfMissingAsync(db, MediumWidthKey, d.MediumWidth.ToString(), "Chiều rộng Medium (Max).", ct);
        await UpsertIfMissingAsync(db, LargeWidthKey, d.LargeWidth.ToString(), "Chiều rộng Large (Max).", ct);
        await UpsertIfMissingAsync(db, WebpQualityKey, d.WebpQuality.ToString(), "Chất lượng WebP preset (icon/thumb/medium/large).", ct);
        await UpsertIfMissingAsync(db, OtfQualityKey, d.OtfQuality.ToString(), "Chất lượng WebP OTF (?width=&format=webp).", ct);

        await ReloadAsync(ct);
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var map = await db.SystemSettings.AsNoTracking()
            .Where(item => ManagedKeys.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase, ct);

        var d = defaults.Value;
        var tune = new MediaTune(
            IconWidth: Clamp(ParseInt(map, IconWidthKey, d.IconWidth), 32, 256),
            ThumbWidth: Clamp(ParseInt(map, ThumbWidthKey, d.ThumbWidth), 100, 900),
            MediumWidth: Clamp(ParseInt(map, MediumWidthKey, d.MediumWidth), 400, 1600),
            LargeWidth: Clamp(ParseInt(map, LargeWidthKey, d.LargeWidth), 800, 2400),
            WebpQuality: Clamp(ParseInt(map, WebpQualityKey, d.WebpQuality), 50, 95),
            OtfQuality: Clamp(ParseInt(map, OtfQualityKey, d.OtfQuality), 50, 95));

        tune = NormalizeOrder(tune);
        Apply(tune);
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(MediaTune input, CancellationToken ct = default)
    {
        var tune = new MediaTune(
            IconWidth: Clamp(input.IconWidth, 32, 256),
            ThumbWidth: Clamp(input.ThumbWidth, 100, 900),
            MediumWidth: Clamp(input.MediumWidth, 400, 1600),
            LargeWidth: Clamp(input.LargeWidth, 800, 2400),
            WebpQuality: Clamp(input.WebpQuality, 50, 95),
            OtfQuality: Clamp(input.OtfQuality, 50, 95));
        tune = NormalizeOrder(tune);

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await UpsertAsync(db, IconWidthKey, tune.IconWidth.ToString(), "Chiều rộng Icon (crop vuông).", ct);
        await UpsertAsync(db, ThumbWidthKey, tune.ThumbWidth.ToString(), "Chiều rộng Thumb (crop vuông).", ct);
        await UpsertAsync(db, MediumWidthKey, tune.MediumWidth.ToString(), "Chiều rộng Medium (Max).", ct);
        await UpsertAsync(db, LargeWidthKey, tune.LargeWidth.ToString(), "Chiều rộng Large (Max).", ct);
        await UpsertAsync(db, WebpQualityKey, tune.WebpQuality.ToString(), "Chất lượng WebP preset (icon/thumb/medium/large).", ct);
        await UpsertAsync(db, OtfQualityKey, tune.OtfQuality.ToString(), "Chất lượng WebP OTF (?width=&format=webp).", ct);
        await db.SaveChangesAsync(ct);

        var prevOtf = Current.OtfQuality;
        Apply(tune);

        if (prevOtf != tune.OtfQuality)
            MediaImageCache.PurgeAll(env);

        return (true, null);
    }

    private void Apply(MediaTune tune)
    {
        lock (_gate)
            _current = tune;
        MediaUrls.SyncDescriptors(tune.IconWidth, tune.ThumbWidth, tune.MediumWidth, tune.LargeWidth);
        MediaImageSharpSetup.OtfQuality = tune.OtfQuality;
    }

    private static MediaTune NormalizeOrder(MediaTune t)
    {
        var thumb = Math.Max(t.ThumbWidth, t.IconWidth);
        var medium = Math.Max(t.MediumWidth, thumb);
        var large = Math.Max(t.LargeWidth, medium);
        return t with { ThumbWidth = thumb, MediumWidth = medium, LargeWidth = large };
    }

    private static async Task UpsertIfMissingAsync(AppDbContext db, string key, string value, string description, CancellationToken ct)
    {
        if (await db.SystemSettings.AnyAsync(item => item.Key == key, ct))
            return;
        db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertAsync(AppDbContext db, string key, string value, string description, CancellationToken ct)
    {
        var row = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == key, ct);
        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
            return;
        }

        row.Value = value;
        row.Description ??= description;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string?> map, string key, int fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return fallback;
        return int.TryParse(raw.Trim(), out var n) ? n : fallback;
    }

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
}

public sealed record MediaTune(
    int IconWidth,
    int ThumbWidth,
    int MediumWidth,
    int LargeWidth,
    int WebpQuality,
    int OtfQuality)
{
    public static MediaTune FromOptions(MediaOptions o) => new(
        o.IconWidth,
        o.ThumbWidth,
        o.MediumWidth,
        o.LargeWidth,
        o.WebpQuality,
        o.OtfQuality);
}
