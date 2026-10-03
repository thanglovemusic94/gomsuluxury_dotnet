using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Đồng bộ bổ sung từ gomsuluxury.vn: chỉ thêm slug/key chưa có (không ghi đè nội dung Admin đã sửa),
/// trừ trang demo "Gốm An Khang" sẽ được thay bằng nội dung gốc.
/// </summary>
public static class SourceSiteSync
{
    private const string Base = "https://gomsuluxury.vn";
    private const string MarkerKey = "SourceSiteSync.LastRun";

    private static readonly HashSet<string> SkipPageSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "trang-chu", "tin-tuc", "shop", "cart", "checkout", "my-account",
        "sample-page", "login-customizer", "video",
        "quay-thuong", "vong-quay", "lucky-wheel", "vong-quay-may-man"
    };

    private static readonly HashSet<string> AllowPageSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "gioi-thieu",
        "ve-chung-toi",
        "chinh-sach-bao-mat",
        "chinh-sach-ban-hang",
        "chinh-sach-doi-tra",
        "chinh-sach-thanh-toan",
        "chinh-sach-giao-hang",
        "cac-loai-tra-shan-tuyet",
        "hoang-kim-sa-kiet-tac-ban-tra"
    };

    public static async Task EnsureAsync(AppDbContext db, ILogger? logger = null)
    {
        try
        {
            var last = await db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Key == MarkerKey);
            if (last is not null &&
                DateTime.TryParse(last.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ranAt) &&
                DateTime.UtcNow - ranAt.ToUniversalTime() < TimeSpan.FromHours(6))
            {
                logger?.LogInformation("SourceSiteSync bỏ qua (đã chạy trong 6 giờ)");
                return;
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WebShop/1.0 (+source-sync)");

            var addedProducts = 0;
            var addedPosts = 0;
            var addedPages = 0;
            try { addedProducts = await SyncProductsAsync(db, client, logger); }
            catch (Exception ex) { logger?.LogError(ex, "Sync products lỗi"); }
            try { addedPosts = await SyncPostsAsync(db, client, logger); }
            catch (Exception ex) { logger?.LogError(ex, "Sync posts lỗi"); }
            try { addedPages = await SyncPagesAsync(db, client, logger); }
            catch (Exception ex) { logger?.LogError(ex, "Sync pages lỗi"); }
            await SyncContactSettingsAsync(db);
            await HtmlBlockSeed.EnsureYoutubePrivacyAsync(db);
            await EnsureFooterMenusAsync(db);
            await ProductCategorySchema.EnsureAsync(db);

            await UpsertSettingAsync(db, MarkerKey, DateTime.UtcNow.ToString("o"),
                $"Sync bổ sung: +{addedProducts} SP, +{addedPosts} bài, +{addedPages} trang");
            await db.SaveChangesAsync();
            logger?.LogInformation("SourceSiteSync xong: +{Products} SP, +{Posts} bài, +{Pages} trang",
                addedProducts, addedPosts, addedPages);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "SourceSiteSync thất bại");
            Console.Error.WriteLine("SourceSiteSync: " + ex.Message);
        }
    }

    private static async Task<int> SyncProductsAsync(AppDbContext db, HttpClient client, ILogger? logger)
    {
        var existing = await db.Products.AsNoTracking().Select(item => item.Slug).ToListAsync();
        var have = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var now = DateTime.UtcNow;

        for (var page = 1; page <= 15; page++)
        {
            var json = await client.GetStringAsync($"{Base}/wp-json/wc/store/products?per_page=20&page={page}");
            var batch = JsonSerializer.Deserialize<List<StoreProduct>>(json, JsonOptions) ?? [];
            if (batch.Count == 0)
                break;

            foreach (var item in batch)
            {
                if (string.IsNullOrWhiteSpace(item.Slug) || item.Images.Count == 0)
                    continue;
                var slug = Cut(item.Slug.Trim(), 255);
                if (have.Contains(slug))
                    continue;

                var sourceCategory = item.Categories.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Slug));
                if (sourceCategory is null)
                    continue;

                var category = await EnsureCategoryAsync(db, categories, sourceCategory);
                var name = Cut(WebUtility.HtmlDecode(item.Name ?? slug), 200);
                var image = item.Images[0].Src ?? "";
                if (image.Length is 0 or > 500)
                    continue;

                var (price, discount) = Prices(item.Prices);
                var description = CleanHtml(item.Description);
                if (string.IsNullOrWhiteSpace(description))
                    description = $"<p>{WebUtility.HtmlEncode(name)}</p>";

                var product = new Product
                {
                    Slug = slug,
                    CategoryId = category.Id,
                    Name = name,
                    Price = price,
                    DiscountPrice = discount,
                    CostPrice = 0,
                    Stock = item.IsInStock ? 15 : 0,
                    ShortDescription = Cut(Plain(item.ShortDescription), 500),
                    Description = description,
                    ImageUrl = image,
                    IsVisible = true,
                    CreatedAt = now.AddMinutes(-added),
                    MetaTitle = Cut(name, 100),
                    MetaDescription = Cut(Plain(item.ShortDescription), 255),
                    SeoFocusKeyword = Cut(name, 100),
                    SeoScore = 70
                };
                db.Products.Add(product);
                await db.SaveChangesAsync();

                db.ProductCategories.Add(new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = category.Id
                });

                var order = 1;
                foreach (var src in item.Images.Skip(1).Take(4)
                             .Select(img => img.Src)
                             .Where(src => !string.IsNullOrWhiteSpace(src) && src!.Length <= 500))
                {
                    db.ProductImages.Add(new ProductImage
                    {
                        ProductId = product.Id,
                        ImageUrl = src!,
                        DisplayOrder = order++,
                        AltText = Cut(name, 200)
                    });
                }

                await db.SaveChangesAsync();
                have.Add(slug);
                added++;
            }
        }

        logger?.LogInformation("SourceSiteSync products +{Count}", added);
        return added;
    }

    private static async Task<int> SyncPostsAsync(AppDbContext db, HttpClient client, ILogger? logger)
    {
        var blogCat = await EnsureBlogCategoryAsync(db);
        var author = await db.Users.AsNoTracking()
            .Where(user => user.Username == AdminSeed.DefaultUsername)
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync();
        if (author is null)
            return 0;

        var existing = await db.Posts.AsNoTracking().Select(item => item.Slug).ToListAsync();
        var have = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        for (var page = 1; page <= 10; page++)
        {
            var url = $"{Base}/wp-json/wp/v2/posts?per_page=20&page={page}&_embed=1";
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                break;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                break;

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var slug = Cut(row.GetProperty("slug").GetString()?.Trim() ?? "", 255);
                if (string.IsNullOrWhiteSpace(slug) || have.Contains(slug))
                    continue;
                if (slug.Contains("quay-thuong", StringComparison.OrdinalIgnoreCase) ||
                    slug.Contains("vong-quay", StringComparison.OrdinalIgnoreCase))
                    continue;

                var title = Cut(Plain(row.GetProperty("title").GetProperty("rendered").GetString()), 200);
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var content = CleanHtml(row.GetProperty("content").GetProperty("rendered").GetString());
                var summary = Cut(Plain(row.GetProperty("excerpt").GetProperty("rendered").GetString()), 500);
                var image = FeaturedImage(row);
                var created = row.TryGetProperty("date", out var dateEl) &&
                              DateTime.TryParse(dateEl.GetString(), out var dt)
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                db.Posts.Add(new Post
                {
                    CategoryId = blogCat.Id,
                    AuthorId = author.Value,
                    Title = title,
                    Slug = slug,
                    Summary = summary,
                    Content = string.IsNullOrWhiteSpace(content) ? $"<p>{WebUtility.HtmlEncode(title)}</p>" : content,
                    ImageUrl = image,
                    IsPublished = true,
                    CreatedAt = created,
                    MetaTitle = Cut(title, 100),
                    MetaDescription = Cut(summary, 255),
                    SeoScore = 65
                });
                have.Add(slug);
                added++;
            }

            await db.SaveChangesAsync();
            if (doc.RootElement.GetArrayLength() < 20)
                break;
        }

        logger?.LogInformation("SourceSiteSync posts +{Count}", added);
        return added;
    }

    private static async Task<int> SyncPagesAsync(AppDbContext db, HttpClient client, ILogger? logger)
    {
        var json = await client.GetStringAsync(
            $"{Base}/wp-json/wp/v2/pages?per_page=100&status=publish&_fields=id,slug,title,content");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return 0;

        var added = 0;
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var slug = Cut(row.GetProperty("slug").GetString()?.Trim() ?? "", 255);
            if (string.IsNullOrWhiteSpace(slug) || SkipPageSlugs.Contains(slug))
                continue;
            if (!AllowPageSlugs.Contains(slug))
                continue;

            var title = Cut(Plain(row.GetProperty("title").GetProperty("rendered").GetString()), 200);
            var content = CleanHtml(row.GetProperty("content").GetProperty("rendered").GetString());
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                continue;

            var existing = await db.CustomPages.FirstOrDefaultAsync(page => page.Slug == slug);
            if (existing is not null)
            {
                // Chỉ thay trang demo cũ, không đụng trang Admin đã soạn thật.
                if (IsDemoPage(existing.Content))
                {
                    existing.Title = title;
                    existing.Content = content;
                    existing.MetaTitle = Cut(title, 100);
                    existing.MetaDescription = Cut(Plain(content), 255);
                    existing.IsPublished = true;
                    added++;
                }

                continue;
            }

            db.CustomPages.Add(new CustomPage
            {
                Title = title,
                Slug = slug,
                Content = content,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                MetaTitle = Cut(title, 100),
                MetaDescription = Cut(Plain(content), 255),
                SeoScore = 60
            });
            added++;
        }

        await db.SaveChangesAsync();
        logger?.LogInformation("SourceSiteSync pages +{Count}", added);
        return added;
    }

    private static async Task SyncContactSettingsAsync(AppDbContext db)
    {
        await FillSettingIfEmptyAsync(db, "FacebookUrl", "https://www.facebook.com/gomsuluxury", "Fanpage Facebook");
        await FillSettingIfEmptyAsync(db, "TikTokUrl", "https://www.tiktok.com/@gomluxury", "TikTok");
        await FillSettingIfEmptyAsync(db, "ZaloUrl", "https://zalo.me/0971966734", "Zalo OA / chat");
        await FillSettingIfEmptyAsync(db, "HotlineAlt", "0837 571 758", "Hotline phụ");
        await FillSettingIfEmptyAsync(db, "ZaloAltUrl", "https://zalo.me/0329116808", "Zalo phụ");
        // Không ghi đè Hotline/Address/SiteName nếu đã có.
        await FillSettingIfEmptyAsync(db, "Address",
            "Số 04 TT2 Foressa 5A, Xuân Phương, Nam Từ Liêm, Hà Nội", "Địa chỉ");
        await FillSettingIfEmptyAsync(db, "BctNote",
            "Website đã thông báo với Bộ Công Thương.",
            "Ghi chú thông báo Bộ Công Thương (hiển thị footer)");
    }

    private static async Task EnsureFooterMenusAsync(AppDbContext db)
    {
        var links = new (string Name, string Url, int Order)[]
        {
            ("Giới thiệu", "/gioi-thieu", 1),
            ("Về chúng tôi", "/ve-chung-toi", 2),
            ("Chính sách giao hàng", "/chinh-sach-giao-hang", 3),
            ("Chính sách đổi trả", "/chinh-sach-doi-tra", 4),
            ("Chính sách bảo mật", "/chinh-sach-bao-mat", 5)
        };

        foreach (var link in links)
        {
            if (!await db.CustomPages.AnyAsync(page => page.Slug == link.Url.TrimStart('/')))
                continue;
            if (await db.Menus.AnyAsync(menu => menu.Position == "Footer" && menu.Url == link.Url))
                continue;

            db.Menus.Add(new Menu
            {
                Name = link.Name,
                Url = link.Url,
                Position = "Footer",
                DisplayOrder = link.Order,
                IsActive = true
            });
        }

        // Header dropdown Về chúng tôi: thêm link nếu thiếu
        var parent = await db.Menus.FirstOrDefaultAsync(menu =>
            menu.Position == "Header" && menu.ParentId == null && menu.Name == "Về chúng tôi");
        if (parent is not null &&
            await db.CustomPages.AnyAsync(page => page.Slug == "ve-chung-toi") &&
            !await db.Menus.AnyAsync(menu => menu.ParentId == parent.Id && menu.Url == "/ve-chung-toi"))
        {
            db.Menus.Add(new Menu
            {
                Name = "Về chúng tôi",
                Url = "/ve-chung-toi",
                ParentId = parent.Id,
                Position = "Header",
                DisplayOrder = 0,
                IsActive = true
            });
        }
    }

    private static bool IsDemoPage(string? content) =>
        !string.IsNullOrWhiteSpace(content) &&
        (content.Contains("Gốm An Khang", StringComparison.OrdinalIgnoreCase) ||
         content.Contains("cửa hàng mẫu", StringComparison.OrdinalIgnoreCase));

    private static string? FeaturedImage(JsonElement row)
    {
        try
        {
            if (!row.TryGetProperty("_embedded", out var embedded))
                return null;
            if (!embedded.TryGetProperty("wp:featuredmedia", out var media) || media.GetArrayLength() == 0)
                return null;
            var url = media[0].GetProperty("source_url").GetString();
            if (string.IsNullOrWhiteSpace(url) || url.Length > 500)
                return null;
            return url;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Category> EnsureBlogCategoryAsync(AppDbContext db)
    {
        var category = await db.Categories.FirstOrDefaultAsync(item => item.Type == "Blog");
        if (category is not null)
            return category;

        category = new Category { Name = "Tin tức", Slug = "tin-tuc", Type = "Blog" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static async Task<Category> EnsureCategoryAsync(AppDbContext db, Dictionary<string, Category> cache, StoreCategory source)
    {
        var slug = Cut(source.Slug!.Trim(), 150);
        if (cache.TryGetValue(slug, out var cached))
            return cached;

        var category = await db.Categories.FirstOrDefaultAsync(item => item.Slug == slug);
        var name = Cut(WebUtility.HtmlDecode(source.Name ?? slug), 100);
        if (category is null)
        {
            category = new Category { Name = name, Slug = slug, Type = "Product" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        cache[slug] = category;
        return category;
    }

    private static async Task FillSettingIfEmptyAsync(AppDbContext db, string key, string value, string description)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == key);
        if (setting is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
            return;
        }

        if (string.IsNullOrWhiteSpace(setting.Value))
        {
            setting.Value = value;
            setting.Description = description;
        }
    }

    private static async Task UpsertSettingAsync(AppDbContext db, string key, string value, string description)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == key);
        if (setting is null)
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
        else
        {
            setting.Value = value;
            setting.Description = description;
        }
    }

    private static (decimal Price, decimal? Discount) Prices(StorePrices? prices)
    {
        if (prices is null)
            return (0, null);
        var current = Money(prices.Price, prices.CurrencyMinorUnit);
        var regular = Money(prices.RegularPrice, prices.CurrencyMinorUnit);
        var sale = Money(prices.SalePrice, prices.CurrencyMinorUnit);
        if (regular <= 0)
            regular = current;
        if (sale > 0 && sale < regular)
            return (regular, sale);
        return (regular > 0 ? regular : current, null);
    }

    private static decimal Money(string? value, int minorUnit)
    {
        if (string.IsNullOrWhiteSpace(value) || !decimal.TryParse(value, out var amount))
            return 0;
        if (minorUnit > 0)
            amount /= (decimal)Math.Pow(10, minorUnit);
        return amount;
    }

    private static string Plain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string CleanHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        var text = Regex.Replace(html, "<script[\\s\\S]*?</script>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<style[\\s\\S]*?</style>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "id=\"ez-toc-container\"[\\s\\S]*?</nav>", "", RegexOptions.IgnoreCase);
        return text.Length <= 50000 ? text : text[..50000];
    }

    private static string Cut(string value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private sealed class StoreProduct
    {
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public bool IsInStock { get; set; }
        public StorePrices? Prices { get; set; }
        public List<StoreImage> Images { get; set; } = [];
        public List<StoreCategory> Categories { get; set; } = [];
    }

    private sealed class StorePrices
    {
        public string? Price { get; set; }
        public string? RegularPrice { get; set; }
        public string? SalePrice { get; set; }
        public int CurrencyMinorUnit { get; set; }
    }

    private sealed class StoreImage
    {
        public string? Src { get; set; }
    }

    private sealed class StoreCategory
    {
        public string? Name { get; set; }
        public string? Slug { get; set; }
    }
}
