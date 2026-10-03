using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class MediaFolderBackfill
{
    public const string MarkerKey = "Media.FoldersBackfilled";

    public static async Task EnsureAsync(AppDbContext db, MediaStorage media, ILogger? logger = null)
    {
        await media.EnsureSchemaAsync();
        if (await db.SystemSettings.AnyAsync(item => item.Key == MarkerKey && item.Value == "1"))
            return;

        var assets = await db.MediaAssets.ToListAsync();
        if (assets.Count == 0)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = MarkerKey,
                Value = "1",
                Description = "Không có media để gán thư mục"
            });
            await db.SaveChangesAsync();
            return;
        }

        var productUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in await db.Products.AsNoTracking().Select(item => item.ImageUrl).ToListAsync())
            AddUrlVariants(productUrls, url);
        foreach (var url in await db.ProductImages.AsNoTracking().Select(item => item.ImageUrl).ToListAsync())
            AddUrlVariants(productUrls, url);

        var newsUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in await db.Posts.AsNoTracking().Select(item => item.ImageUrl).ToListAsync())
            AddUrlVariants(newsUrls, url);

        var sliderUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slide in await ShopSlides.LoadAsync(db))
            AddUrlVariants(sliderUrls, slide.ImageUrl);

        var updated = 0;
        foreach (var asset in assets)
        {
            var folder = MediaFolders.Other;
            if (Matches(asset, productUrls))
                folder = MediaFolders.Products;
            else if (Matches(asset, newsUrls))
                folder = MediaFolders.News;
            else if (Matches(asset, sliderUrls))
                folder = MediaFolders.Slider;

            var current = string.IsNullOrWhiteSpace(asset.Folder)
                ? MediaFolders.Other
                : MediaFolders.Normalize(asset.Folder);
            if (current == MediaFolders.All)
                current = MediaFolders.Other;

            if (current == MediaFolders.Other && folder != MediaFolders.Other)
            {
                asset.Folder = folder;
                asset.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else if (string.IsNullOrWhiteSpace(asset.Folder))
            {
                asset.Folder = MediaFolders.Other;
                updated++;
            }
        }

        db.SystemSettings.Add(new SystemSetting
        {
            Key = MarkerKey,
            Value = "1",
            Description = $"Đã gán thư mục media ({updated} ảnh cập nhật)"
        });
        await db.SaveChangesAsync();
        logger?.LogInformation("Media folder backfill: {Count} ảnh cập nhật.", updated);
    }

    private static bool Matches(MediaAsset asset, HashSet<string> urls) =>
        urls.Contains(asset.OriginalUrl)
        || (!string.IsNullOrWhiteSpace(asset.ThumbUrl) && urls.Contains(asset.ThumbUrl))
        || (!string.IsNullOrWhiteSpace(asset.MediumUrl) && urls.Contains(asset.MediumUrl))
        || (!string.IsNullOrWhiteSpace(asset.LargeUrl) && urls.Contains(asset.LargeUrl));

    private static void AddUrlVariants(HashSet<string> set, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        set.Add(url);
        set.Add(MediaUrls.For(url, MediaSize.Thumb));
        set.Add(MediaUrls.For(url, MediaSize.Medium));
        set.Add(MediaUrls.For(url, MediaSize.Large));
        set.Add(MediaUrls.For(url, MediaSize.Original));
    }
}
