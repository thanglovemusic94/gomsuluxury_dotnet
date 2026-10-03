using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class RemoteImageImport
{
    public const string MarkerKey = "Media.RemoteImagesImported";
    public const string SlidesKey = "ShopSlides.Json";

    public static async Task EnsureAsync(AppDbContext db, MediaStorage media, IHttpClientFactory httpFactory, ILogger? logger = null)
    {
        if (await db.SystemSettings.AnyAsync(item => item.Key == MarkerKey && item.Value == "1"))
            return;

        await media.EnsureSchemaAsync();
        using var http = httpFactory.CreateClient("media-import");
        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var okCount = 0;
        var failCount = 0;

        async Task<string?> ResolveAsync(string? remote, string? alt, MediaSize size, string folder)
        {
            if (string.IsNullOrWhiteSpace(remote))
                return remote;
            if (remote.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return remote;
            if (!remote.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !remote.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return remote;

            if (cache.TryGetValue(remote, out var cached))
                return cached;

            try
            {
                var (ok, url, error, _) = await media.ImportFromUrlAsync(http, remote, alt, size, folder);
                if (!ok || string.IsNullOrWhiteSpace(url))
                {
                    failCount++;
                    logger?.LogWarning("Import ảnh thất bại: {Url} — {Error}", remote, error);
                    cache[remote] = remote;
                    return remote;
                }

                okCount++;
                cache[remote] = url;
                return url;
            }
            catch (Exception ex)
            {
                failCount++;
                logger?.LogWarning(ex, "Import ảnh lỗi: {Url}", remote);
                cache[remote] = remote;
                return remote;
            }
        }

        var products = await db.Products.ToListAsync();
        foreach (var product in products)
        {
            var next = await ResolveAsync(product.ImageUrl, product.Name, MediaSize.Medium, MediaFolders.Products);
            if (!string.IsNullOrWhiteSpace(next) && next.Length <= 500)
                product.ImageUrl = next;
        }

        var gallery = await db.ProductImages.ToListAsync();
        foreach (var image in gallery)
        {
            var next = await ResolveAsync(image.ImageUrl, image.AltText, MediaSize.Medium, MediaFolders.Products);
            if (!string.IsNullOrWhiteSpace(next) && next.Length <= 500)
                image.ImageUrl = next;
        }

        var posts = await db.Posts.ToListAsync();
        foreach (var post in posts)
        {
            if (string.IsNullOrWhiteSpace(post.ImageUrl))
                continue;
            var next = await ResolveAsync(post.ImageUrl, post.Title, MediaSize.Medium, MediaFolders.News);
            if (!string.IsNullOrWhiteSpace(next) && next.Length <= 500)
                post.ImageUrl = next;
        }

        var logo = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == "LogoUrl");
        if (logo is not null && !string.IsNullOrWhiteSpace(logo.Value))
        {
            var next = await ResolveAsync(logo.Value, "Logo", MediaSize.Medium, MediaFolders.Other);
            if (!string.IsNullOrWhiteSpace(next))
                logo.Value = next;
        }

        var importedSlides = new List<ShopSlide>();
        foreach (var slide in ShopSlides.Default)
        {
            var next = await ResolveAsync(slide.ImageUrl, slide.Alt, MediaSize.Large, MediaFolders.Slider);
            importedSlides.Add(new ShopSlide
            {
                ImageUrl = next ?? slide.ImageUrl,
                LinkUrl = slide.LinkUrl,
                Alt = slide.Alt
            });
        }

        var slidesJson = JsonSerializer.Serialize(importedSlides);
        var slidesSetting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == SlidesKey);
        if (slidesSetting is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = SlidesKey,
                Value = slidesJson,
                Description = "Slider trang chủ (JSON)"
            });
        }
        else
        {
            slidesSetting.Value = slidesJson;
        }

        db.SystemSettings.Add(new SystemSetting
        {
            Key = MarkerKey,
            Value = "1",
            Description = $"Đã nhập ảnh remote → local ({okCount} ok, {failCount} lỗi)"
        });

        await db.SaveChangesAsync();
        logger?.LogInformation("Remote image import xong: {Ok} thành công, {Fail} lỗi.", okCount, failCount);
    }
}
