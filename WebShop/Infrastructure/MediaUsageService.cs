using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public sealed record MediaUsageRef(
    string Kind,
    string Label,
    string? Link,
    int? EntityId);

/// <summary>
/// Single place that knows where media URLs appear in shop content.
/// Used by Admin (where-used), SEO remap, and garbage collection.
/// </summary>
public sealed class MediaUsageService(IServiceScopeFactory scopes)
{
    private static readonly Regex UploadUrlRegex = new(
        @"/uploads/(?:optimized/(?:icon|thumb|medium|large)/|originals/)?[^\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<HashSet<string>> CollectReferencedUrlsAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await CollectReferencedUrlsCoreAsync(db, ct);
    }

    /// <summary>Logo + slides — never auto soft-delete these assets.</summary>
    public async Task<HashSet<string>> CollectProtectedUrlsAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in await db.SystemSettings.AsNoTracking()
                     .Where(item => item.Key == RemoteImageImport.SlidesKey || item.Key == "LogoUrl")
                     .Select(item => item.Value)
                     .ToListAsync(ct))
            AddUrlsFromText(set, row);

        return set;
    }

    public async Task<IReadOnlyList<MediaUsageRef>> GetUsagesAsync(int assetId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(item => item.Id == assetId, ct);
        if (asset is null)
            return [];

        return await GetUsagesForAssetAsync(db, asset, ct);
    }

    public async Task<Dictionary<int, int>> CountUsagesAsync(IReadOnlyList<int> assetIds, CancellationToken ct = default)
    {
        var result = new Dictionary<int, int>();
        if (assetIds.Count == 0)
            return result;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assets = await db.MediaAssets.AsNoTracking()
            .Where(item => assetIds.Contains(item.Id))
            .ToListAsync(ct);

        foreach (var asset in assets)
        {
            var usages = await GetUsagesForAssetAsync(db, asset, ct);
            result[asset.Id] = usages.Count;
        }

        return result;
    }

    /// <summary>
    /// Asset IDs that appear in content or are protected (logo/slides).
    /// </summary>
    public async Task<HashSet<int>> GetReferencedOrProtectedAssetIdsAsync(
        IReadOnlyList<MediaAsset> candidates,
        CancellationToken ct = default)
    {
        var referenced = await CollectReferencedUrlsAsync(ct);
        var protectedUrls = await CollectProtectedUrlsAsync(ct);
        var hit = new HashSet<int>();

        foreach (var asset in candidates)
        {
            if (AssetTouchesUrlSet(asset, protectedUrls) || AssetTouchesUrlSet(asset, referenced))
                hit.Add(asset.Id);
        }

        return hit;
    }

    private static async Task<IReadOnlyList<MediaUsageRef>> GetUsagesForAssetAsync(
        AppDbContext db,
        MediaAsset asset,
        CancellationToken ct)
    {
        var keys = AssetMatchKeys(asset);
        var list = new List<MediaUsageRef>();

        foreach (var row in await db.Products.AsNoTracking()
                     .Select(item => new { item.Id, item.Name, item.Slug, item.ImageUrl, item.SeoImage, item.Description, item.ShortDescription })
                     .ToListAsync(ct))
        {
            if (TextMatches(keys, row.ImageUrl, row.SeoImage, row.Description, row.ShortDescription))
                list.Add(new MediaUsageRef("Product", row.Name, "/Admin/Products/Edit/" + row.Id, row.Id));
        }

        foreach (var row in await db.ProductImages.AsNoTracking()
                     .Select(item => new { item.Id, item.ProductId, item.ImageUrl })
                     .ToListAsync(ct))
        {
            if (TextMatches(keys, row.ImageUrl))
                list.Add(new MediaUsageRef("ProductImage", $"Ảnh phụ SP #{row.ProductId}", "/Admin/Products/Edit/" + row.ProductId, row.ProductId));
        }

        foreach (var row in await db.Posts.AsNoTracking()
                     .Select(item => new { item.Id, item.Title, item.Slug, item.ImageUrl, item.SeoImage, item.Content })
                     .ToListAsync(ct))
        {
            if (TextMatches(keys, row.ImageUrl, row.SeoImage, row.Content))
                list.Add(new MediaUsageRef("Post", row.Title, "/Admin/Posts/Edit/" + row.Id, row.Id));
        }

        foreach (var row in await db.CustomPages.AsNoTracking()
                     .Select(item => new { item.Id, item.Title, item.Slug, item.SeoImage, item.Content })
                     .ToListAsync(ct))
        {
            if (TextMatches(keys, row.SeoImage, row.Content))
                list.Add(new MediaUsageRef("Page", row.Title, "/Admin/Pages/Edit/" + row.Id, row.Id));
        }

        foreach (var row in await db.HtmlBlocks.AsNoTracking()
                     .Select(item => new { item.Id, item.Key, item.Content })
                     .ToListAsync(ct))
        {
            if (TextMatches(keys, row.Content))
                list.Add(new MediaUsageRef("HtmlBlock", row.Key, "/Admin/HtmlBlocks", row.Id));
        }

        var logo = await db.SystemSettings.AsNoTracking()
            .Where(item => item.Key == "LogoUrl")
            .Select(item => item.Value)
            .FirstOrDefaultAsync(ct);
        if (TextMatches(keys, logo))
            list.Add(new MediaUsageRef("Setting", "LogoUrl", "/Admin/Settings", null));

        var slidesJson = await db.SystemSettings.AsNoTracking()
            .Where(item => item.Key == RemoteImageImport.SlidesKey)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(ct);
        if (TextMatches(keys, slidesJson))
            list.Add(new MediaUsageRef("Setting", "Slider (ShopSlides)", "/Admin/Settings", null));

        return list;
    }

    private static async Task<HashSet<string>> CollectReferencedUrlsCoreAsync(AppDbContext db, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in await db.Products.AsNoTracking()
                     .Select(item => new { item.ImageUrl, item.SeoImage, item.Description, item.ShortDescription })
                     .ToListAsync(ct))
        {
            AddUrlsFromText(set, row.ImageUrl);
            AddUrlsFromText(set, row.SeoImage);
            AddUrlsFromText(set, row.Description);
            AddUrlsFromText(set, row.ShortDescription);
        }

        foreach (var row in await db.ProductImages.AsNoTracking().Select(item => item.ImageUrl).ToListAsync(ct))
            AddUrlsFromText(set, row);

        foreach (var row in await db.Posts.AsNoTracking()
                     .Select(item => new { item.ImageUrl, item.SeoImage, item.Content })
                     .ToListAsync(ct))
        {
            AddUrlsFromText(set, row.ImageUrl);
            AddUrlsFromText(set, row.SeoImage);
            AddUrlsFromText(set, row.Content);
        }

        foreach (var row in await db.CustomPages.AsNoTracking()
                     .Select(item => new { item.SeoImage, item.Content })
                     .ToListAsync(ct))
        {
            AddUrlsFromText(set, row.SeoImage);
            AddUrlsFromText(set, row.Content);
        }

        foreach (var row in await db.HtmlBlocks.AsNoTracking().Select(item => item.Content).ToListAsync(ct))
            AddUrlsFromText(set, row);

        foreach (var row in await db.SystemSettings.AsNoTracking()
                     .Where(item => item.Key == RemoteImageImport.SlidesKey || item.Key == "LogoUrl")
                     .Select(item => item.Value)
                     .ToListAsync(ct))
            AddUrlsFromText(set, row);

        return set;
    }

    private static void AddUrlsFromText(HashSet<string> set, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            set.Add(value.TrimEnd('.', ',', ';', ')', ']'));

        foreach (Match match in UploadUrlRegex.Matches(value))
            set.Add(match.Value.TrimEnd('.', ',', ';', ')', ']'));
    }

    private static HashSet<string> AssetMatchKeys(MediaAsset asset)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;
            keys.Add(url);
            var file = Path.GetFileName(url);
            if (!string.IsNullOrWhiteSpace(file))
                keys.Add(file);
        }

        Add(asset.OriginalUrl);
        Add(asset.ThumbUrl);
        Add(asset.MediumUrl);
        Add(asset.LargeUrl);
        if (!string.IsNullOrWhiteSpace(asset.MediumUrl))
            Add(MediaUrls.For(asset.MediumUrl, MediaSize.Icon));
        else if (!string.IsNullOrWhiteSpace(asset.ThumbUrl))
            Add(MediaUrls.For(asset.ThumbUrl, MediaSize.Icon));

        if (!string.IsNullOrWhiteSpace(asset.FileName))
            keys.Add(Path.GetFileName(asset.FileName));

        return keys;
    }

    private static bool TextMatches(HashSet<string> keys, params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            if (keys.Contains(value))
                return true;

            foreach (Match match in UploadUrlRegex.Matches(value))
            {
                var url = match.Value.TrimEnd('.', ',', ';', ')', ']');
                if (keys.Contains(url))
                    return true;
                var file = Path.GetFileName(url);
                if (!string.IsNullOrWhiteSpace(file) && keys.Contains(file))
                    return true;
            }

            var bareFile = Path.GetFileName(value);
            if (!string.IsNullOrWhiteSpace(bareFile) && keys.Contains(bareFile))
                return true;
        }

        return false;
    }

    private static bool AssetTouchesUrlSet(MediaAsset asset, HashSet<string> urls)
    {
        var keys = AssetMatchKeys(asset);
        foreach (var url in urls)
        {
            if (keys.Contains(url))
                return true;
            var file = Path.GetFileName(url);
            if (!string.IsNullOrWhiteSpace(file) && keys.Contains(file))
                return true;
        }

        return false;
    }
}
