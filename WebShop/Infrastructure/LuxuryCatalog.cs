using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class LuxuryCatalog
{
    private const string MarkerKey = "SourceCatalog";

    public static async Task EnsureAsync(AppDbContext db)
    {
        if (await db.SystemSettings.AnyAsync(item => item.Key == MarkerKey && item.Value == "1"))
            return;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WebShop/1.0");
            var products = await LoadProductsAsync(client);
            if (products.Count == 0)
                return;

            var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
            var index = 0;
            foreach (var item in products)
            {
                var sourceCategory = item.Categories.FirstOrDefault(category => !string.IsNullOrWhiteSpace(category.Slug));
                if (sourceCategory is null || string.IsNullOrWhiteSpace(item.Slug) || item.Images.Count == 0)
                    continue;

                var category = await EnsureCategoryAsync(db, categories, sourceCategory);
                var slug = Cut(item.Slug.Trim(), 255);
                var name = Cut(WebUtility.HtmlDecode(item.Name ?? slug), 200);
                var image = item.Images[0].Src ?? "";
                if (image.Length == 0 || image.Length > 500)
                    continue;

                var (price, discount) = Prices(item.Prices);
                var description = CleanHtml(item.Description);
                if (string.IsNullOrWhiteSpace(description))
                    description = $"<p>{WebUtility.HtmlEncode(name)}</p>";

                var product = await db.Products.FirstOrDefaultAsync(row => row.Slug == slug);
                if (product is null)
                {
                    product = new Product { Slug = slug, CreatedAt = now.AddMinutes(-index) };
                    db.Products.Add(product);
                }

                product.CategoryId = category.Id;
                product.Name = name;
                product.Price = price;
                product.DiscountPrice = discount;
                product.CostPrice = 0;
                product.Stock = item.IsInStock ? 15 : 0;
                product.ShortDescription = Cut(Plain(item.ShortDescription), 500);
                product.Description = description;
                product.ImageUrl = image;
                product.IsVisible = true;
                product.MetaTitle = Cut(name, 100);
                product.MetaDescription = Cut(Plain(item.ShortDescription), 255);
                product.SeoFocusKeyword = Cut(name, 100);
                product.SeoScore = 70;
                await db.SaveChangesAsync();

                var extras = item.Images.Skip(1).Take(4).Select(image => image.Src).Where(src => !string.IsNullOrWhiteSpace(src) && src!.Length <= 500).ToList();
                var oldImages = await db.ProductImages.Where(image => image.ProductId == product.Id).ToListAsync();
                db.ProductImages.RemoveRange(oldImages);
                var order = 1;
                foreach (var src in extras)
                {
                    db.ProductImages.Add(new ProductImage
                    {
                        ProductId = product.Id,
                        ImageUrl = src!,
                        DisplayOrder = order++,
                        AltText = Cut(name, 200)
                    });
                }

                index++;
            }

            var samples = await db.Products.Where(item => item.ImageUrl.Contains("unsplash.com")).ToListAsync();
            foreach (var sample in samples)
                sample.IsVisible = false;

            await SyncMenusAsync(db, categories.Values);
            await UpsertSettingAsync(db, "SiteName", "Gốm Sứ Luxury", "Tên cửa hàng");
            await UpsertSettingAsync(db, "Hotline", "0971 966 734", "Số điện thoại");
            await UpsertSettingAsync(db, "Address", "Khu đô thị Viglacera Xuân Phương, Nam Từ Liêm, Hà Nội", "Địa chỉ");
            await UpsertSettingAsync(db, "LogoUrl", "https://gomsuluxury.vn/wp-content/uploads/2024/03/LOGO-WEB-1.png", "Logo");
            await UpsertSettingAsync(db, MarkerKey, "1", "Đã nạp sản phẩm từ website hiện tại");
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Không nạp được catalog: " + ex.Message);
        }
    }

    private static async Task<List<StoreProduct>> LoadProductsAsync(HttpClient client)
    {
        var all = new List<StoreProduct>();
        for (var page = 1; page <= 10; page++)
        {
            var json = await client.GetStringAsync($"https://gomsuluxury.vn/wp-json/wc/store/products?per_page=20&page={page}");
            var batch = JsonSerializer.Deserialize<List<StoreProduct>>(json, JsonOptions) ?? [];
            if (batch.Count == 0)
                break;
            all.AddRange(batch);
        }

        return all;
    }

    private static async Task<Category> EnsureCategoryAsync(AppDbContext db, Dictionary<string, Category> cache, StoreCategory source)
    {
        var slug = Cut(source.Slug.Trim(), 150);
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
        else if (category.Name != name)
        {
            category.Name = name;
            category.Type = "Product";
        }

        cache[slug] = category;
        return category;
    }

    private static async Task SyncMenusAsync(AppDbContext db, IEnumerable<Category> categories)
    {
        var parent = await db.Menus.FirstOrDefaultAsync(menu => menu.Position == "Header" && (menu.Url == "/san-pham" || menu.Url == "san-pham"));
        if (parent is null)
        {
            parent = new Menu { Name = "Sản phẩm", Url = "/san-pham", Position = "Header", DisplayOrder = 3, IsActive = true };
            db.Menus.Add(parent);
            await db.SaveChangesAsync();
        }

        var children = await db.Menus.Where(menu => menu.ParentId == parent.Id).ToListAsync();
        foreach (var child in children)
            child.IsActive = false;

        var order = 1;
        foreach (var category in categories.OrderBy(Rank).ThenBy(item => item.Name))
        {
            var url = "/danh-muc/" + category.Slug;
            var menu = children.FirstOrDefault(item => item.Url == url);
            if (menu is null)
            {
                db.Menus.Add(new Menu
                {
                    Name = category.Name,
                    Url = url,
                    ParentId = parent.Id,
                    Position = "Header",
                    DisplayOrder = order,
                    IsActive = true
                });
            }
            else
            {
                menu.Name = category.Name;
                menu.DisplayOrder = order;
                menu.IsActive = true;
                menu.Position = "Header";
            }

            order++;
        }
    }

    private static int Rank(Category category) => category.Slug switch
    {
        "am-chen-bat-trang" => 0,
        "khay-tra" => 1,
        "phu-kien-ban-tra-gom-su-luxury" => 2,
        "lu-xong-tram" => 3,
        "hu-tra" => 4,
        "tuong-gom-linh-vat" => 5,
        "vong-tay" => 6,
        _ => 9
    };

    private static async Task UpsertSettingAsync(AppDbContext db, string key, string value, string description)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == key);
        if (setting is null)
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
        else
            setting.Value = value;
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
        return text.Length <= 20000 ? text : text[..20000];
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

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
