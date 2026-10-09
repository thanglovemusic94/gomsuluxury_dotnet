using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Tải ảnh remote (đặc biệt gomsuluxury.vn/wp-content) vào /uploads và thay URL trong DB.
/// Chạy idempotent khi còn URL remote; bỏ qua nhanh nếu không còn gì.
/// </summary>
public static partial class RemoteImageImport
{
    public const string MarkerKey = "Media.RemoteImagesImported";
    public const string SlidesKey = "ShopSlides.Json";

    private const string SourceHostHint = "gomsuluxury.vn/wp-content";

    [GeneratedRegex(
        @"https?://(?:www\.)?gomsuluxury\.vn/wp-content/[^\s""'<>\\]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WpContentUrlRegex();

    [GeneratedRegex(
        @"//(?:www\.)?gomsuluxury\.vn/wp-content/[^\s""'<>\\]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WpContentProtocolRelativeRegex();

    public static async Task EnsureAsync(
        AppDbContext db, MediaStorage media, IHttpClientFactory httpFactory, ILogger? logger = null)
    {
        // Fast path: nothing left to import.
        if (!await HasPendingRemoteAsync(db))
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

            remote = WebUtility.HtmlDecode(remote.Trim());
            if (remote.StartsWith("//", StringComparison.Ordinal))
                remote = "https:" + remote;

            if (remote.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return remote;
            if (!remote.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !remote.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return remote;

            // Only pull source-site media (and any leftover remote image fields on that host).
            if (!remote.Contains(SourceHostHint, StringComparison.OrdinalIgnoreCase)
                && !remote.Contains("gomsuluxury.vn/", StringComparison.OrdinalIgnoreCase))
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

        async Task<string?> RewriteHtmlAsync(string? html, string? alt, string folder)
        {
            if (string.IsNullOrWhiteSpace(html)
                || !html.Contains("gomsuluxury.vn", StringComparison.OrdinalIgnoreCase))
                return html;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in WpContentUrlRegex().Matches(html))
            {
                var raw = match.Value.TrimEnd('.', ',', ';', ')', ']');
                if (map.ContainsKey(raw))
                    continue;
                var local = await ResolveAsync(raw, alt, MediaSize.Medium, folder);
                if (!string.IsNullOrWhiteSpace(local) && !string.Equals(local, raw, StringComparison.Ordinal))
                    map[raw] = local!;
            }

            foreach (Match match in WpContentProtocolRelativeRegex().Matches(html))
            {
                var raw = match.Value.TrimEnd('.', ',', ';', ')', ']');
                var absolute = "https:" + raw;
                if (map.ContainsKey(raw) || map.ContainsKey(absolute))
                    continue;
                var local = await ResolveAsync(absolute, alt, MediaSize.Medium, folder);
                if (!string.IsNullOrWhiteSpace(local) && !string.Equals(local, absolute, StringComparison.Ordinal))
                {
                    map[raw] = local!;
                    map[absolute] = local!;
                }
            }

            if (map.Count == 0)
                return html;

            var next = html;
            foreach (var (from, to) in map.OrderByDescending(item => item.Key.Length))
                next = next.Replace(from, to, StringComparison.OrdinalIgnoreCase);
            return next;
        }

        var products = await db.Products.IgnoreQueryFilters().ToListAsync();
        foreach (var product in products)
        {
            var nextImg = await ResolveAsync(product.ImageUrl, product.Name, MediaSize.Medium, MediaFolders.Products);
            if (!string.IsNullOrWhiteSpace(nextImg) && nextImg.Length <= 500)
                product.ImageUrl = nextImg;

            if (!string.IsNullOrWhiteSpace(product.SeoImage))
            {
                var nextSeo = await ResolveAsync(product.SeoImage, product.Name, MediaSize.Large, MediaFolders.Products);
                if (!string.IsNullOrWhiteSpace(nextSeo) && nextSeo.Length <= 500)
                    product.SeoImage = nextSeo;
            }

            product.Description = await RewriteHtmlAsync(product.Description, product.Name, MediaFolders.Products) ?? product.Description;
            product.ShortDescription = await RewriteHtmlAsync(product.ShortDescription, product.Name, MediaFolders.Products)
                                       ?? product.ShortDescription;
        }

        var gallery = await db.ProductImages.ToListAsync();
        foreach (var image in gallery)
        {
            var next = await ResolveAsync(image.ImageUrl, image.AltText, MediaSize.Medium, MediaFolders.Products);
            if (!string.IsNullOrWhiteSpace(next) && next.Length <= 500)
                image.ImageUrl = next;
        }

        var posts = await db.Posts.IgnoreQueryFilters().ToListAsync();
        foreach (var post in posts)
        {
            if (!string.IsNullOrWhiteSpace(post.ImageUrl))
            {
                var next = await ResolveAsync(post.ImageUrl, post.Title, MediaSize.Medium, MediaFolders.News);
                if (!string.IsNullOrWhiteSpace(next) && next.Length <= 500)
                    post.ImageUrl = next;
            }

            if (!string.IsNullOrWhiteSpace(post.SeoImage))
            {
                var nextSeo = await ResolveAsync(post.SeoImage, post.Title, MediaSize.Large, MediaFolders.News);
                if (!string.IsNullOrWhiteSpace(nextSeo) && nextSeo.Length <= 500)
                    post.SeoImage = nextSeo;
            }

            post.Content = await RewriteHtmlAsync(post.Content, post.Title, MediaFolders.News) ?? post.Content;
            post.Summary = await RewriteHtmlAsync(post.Summary, post.Title, MediaFolders.News) ?? post.Summary;
        }

        var pages = await db.CustomPages.IgnoreQueryFilters().ToListAsync();
        foreach (var page in pages)
        {
            if (!string.IsNullOrWhiteSpace(page.SeoImage))
            {
                var nextSeo = await ResolveAsync(page.SeoImage, page.Title, MediaSize.Large, MediaFolders.Other);
                if (!string.IsNullOrWhiteSpace(nextSeo) && nextSeo.Length <= 500)
                    page.SeoImage = nextSeo;
            }

            page.Content = await RewriteHtmlAsync(page.Content, page.Title, MediaFolders.Pages) ?? page.Content;
        }

        var blocks = await db.HtmlBlocks.ToListAsync();
        foreach (var block in blocks)
            block.Content = await RewriteHtmlAsync(block.Content, block.Title, MediaFolders.Pages) ?? block.Content;

        var reviews = await db.ProductReviews.Where(item => item.ImageUrl != null && item.ImageUrl.Contains("gomsuluxury")).ToListAsync();
        foreach (var review in reviews)
        {
            var next = await ResolveAsync(review.ImageUrl, review.CustomerName, MediaSize.Medium, MediaFolders.Products);
            if (!string.IsNullOrWhiteSpace(next) && next.Length <= 500)
                review.ImageUrl = next;
        }

        var landings = await db.LandingPages.ToListAsync();
        foreach (var lp in landings)
        {
            var alt = string.IsNullOrWhiteSpace(lp.Headline) ? lp.Name : lp.Headline;
            if (!string.IsNullOrWhiteSpace(lp.HeroImageUrl))
            {
                var next = await ResolveAsync(lp.HeroImageUrl, alt, MediaSize.Large, MediaFolders.Other);
                if (!string.IsNullOrWhiteSpace(next) && next.Length <= 500)
                    lp.HeroImageUrl = next;
            }

            if (!string.IsNullOrWhiteSpace(lp.SeoImage))
            {
                var nextSeo = await ResolveAsync(lp.SeoImage, alt, MediaSize.Large, MediaFolders.Other);
                if (!string.IsNullOrWhiteSpace(nextSeo) && nextSeo.Length <= 500)
                    lp.SeoImage = nextSeo;
            }

            lp.BodyHtml = await RewriteHtmlAsync(lp.BodyHtml, alt, MediaFolders.Other) ?? lp.BodyHtml;
        }

        foreach (var key in new[] { "LogoUrl", "FaviconUrl", "OgImageUrl" })
        {
            var row = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == key);
            if (row is null || string.IsNullOrWhiteSpace(row.Value))
                continue;
            var next = await ResolveAsync(row.Value, key, MediaSize.Medium, MediaFolders.Other);
            if (!string.IsNullOrWhiteSpace(next))
                row.Value = next;
        }

        var slidesSetting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == SlidesKey);
        if (slidesSetting is not null
            && !string.IsNullOrWhiteSpace(slidesSetting.Value)
            && slidesSetting.Value.Contains("gomsuluxury", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var slides = JsonSerializer.Deserialize<List<ShopSlide>>(slidesSetting.Value) ?? [];
                var rewritten = new List<ShopSlide>(slides.Count);
                foreach (var slide in slides)
                {
                    var next = await ResolveAsync(slide.ImageUrl, slide.Alt, MediaSize.Large, MediaFolders.Slider);
                    rewritten.Add(new ShopSlide
                    {
                        ImageUrl = next ?? slide.ImageUrl,
                        LinkUrl = slide.LinkUrl,
                        Alt = slide.Alt
                    });
                }

                slidesSetting.Value = JsonSerializer.Serialize(rewritten);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Không rewrite được {Key}", SlidesKey);
            }
        }
        else if (slidesSetting is null)
        {
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

            db.SystemSettings.Add(new SystemSetting
            {
                Key = SlidesKey,
                Value = JsonSerializer.Serialize(importedSlides),
                Description = "Slider trang chủ (JSON)"
            });
        }

        var marker = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == MarkerKey);
        var summary = $"Đã nhập ảnh remote → local ({okCount} ok, {failCount} lỗi) lúc {DateTime.UtcNow:u}";
        if (marker is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = MarkerKey,
                Value = okCount > 0 || failCount == 0 ? "1" : "0",
                Description = summary
            });
        }
        else
        {
            marker.Value = "1";
            marker.Description = summary;
        }

        await db.SaveChangesAsync();
        logger?.LogInformation("Remote image import xong: {Ok} thành công, {Fail} lỗi.", okCount, failCount);
    }

    private static async Task<bool> HasPendingRemoteAsync(AppDbContext db)
    {
        const string marker = "gomsuluxury.vn/wp-content";

        if (await db.Products.IgnoreQueryFilters().AnyAsync(item =>
                item.ImageUrl.Contains(marker)
                || item.Description.Contains(marker)
                || (item.ShortDescription != null && item.ShortDescription.Contains(marker))
                || (item.SeoImage != null && item.SeoImage.Contains(marker))))
            return true;
        if (await db.ProductImages.AnyAsync(item => item.ImageUrl.Contains(marker)))
            return true;
        if (await db.Posts.IgnoreQueryFilters().AnyAsync(item =>
                (item.ImageUrl != null && item.ImageUrl.Contains(marker))
                || item.Content.Contains(marker)
                || (item.Summary != null && item.Summary.Contains(marker))
                || (item.SeoImage != null && item.SeoImage.Contains(marker))))
            return true;
        if (await db.CustomPages.IgnoreQueryFilters().AnyAsync(item =>
                item.Content.Contains(marker)
                || (item.SeoImage != null && item.SeoImage.Contains(marker))))
            return true;
        if (await db.HtmlBlocks.AnyAsync(item => item.Content.Contains(marker)))
            return true;
        if (await db.ProductReviews.AnyAsync(item => item.ImageUrl != null && item.ImageUrl.Contains(marker)))
            return true;
        if (await db.LandingPages.AnyAsync(item =>
                (item.HeroImageUrl != null && item.HeroImageUrl.Contains(marker))
                || (item.BodyHtml != null && item.BodyHtml.Contains(marker))
                || (item.SeoImage != null && item.SeoImage.Contains(marker))))
            return true;
        if (await db.SystemSettings.AnyAsync(item => item.Value.Contains(marker)))
            return true;

        // First run: default slides still point at source CDN.
        if (!await db.SystemSettings.AnyAsync(item => item.Key == MarkerKey))
            return ShopSlides.Default.Any(slide =>
                slide.ImageUrl.Contains(marker, StringComparison.OrdinalIgnoreCase));

        return false;
    }
}
