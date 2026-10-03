using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class DemoCatalog
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        var marker = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == "DemoCatalog");
        if (marker?.Value == "2")
            return;

        if (marker is not null)
        {
            await AlignImagesAsync(db);
            marker.Value = "2";
            await db.SaveChangesAsync();
            return;
        }

        var admin = await db.Users.FirstOrDefaultAsync(user => user.Username == AdminSeed.DefaultUsername);
        if (admin is null)
            return;

        await HidePlaceholdersAsync(db);
        await RenamePlaceholderCategoryAsync(db, "danh-muc-1", "Bộ ấm chén", "bo-am-chen");
        await RenamePlaceholderCategoryAsync(db, "danh-muc-2", "Phụ kiện bàn trà", "phu-kien-ban-tra");

        var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in new (string Name, string Slug, string Type)[]
        {
            ("Bộ ấm chén", "bo-am-chen", "Product"),
            ("Khay trà", "khay-tra", "Product"),
            ("Phụ kiện bàn trà", "phu-kien-ban-tra", "Product"),
            ("Lư xông trầm", "lu-xong-tram", "Product"),
            ("Hũ trà", "hu-tra", "Product"),
            ("Tin tức", "tin-tuc", "Blog")
        })
        {
            categories[item.Slug] = await EnsureCategoryAsync(db, item.Name, item.Slug, item.Type);
        }

        var now = DateTime.UtcNow;
        var index = 0;
        foreach (var item in Products)
        {
            if (await db.Products.AnyAsync(product => product.Slug == item.Slug))
                continue;

            db.Products.Add(new Product
            {
                CategoryId = categories[item.CategorySlug].Id,
                Name = item.Name,
                Slug = item.Slug,
                Price = item.Price,
                DiscountPrice = item.Discount,
                CostPrice = item.Cost,
                Stock = item.Stock,
                ShortDescription = item.Short,
                Description = $"<p>{item.Short}</p><p>{item.Detail}</p>",
                ImageUrl = item.Image,
                IsVisible = true,
                CreatedAt = now.AddMinutes(-index),
                MetaTitle = item.Name,
                MetaDescription = item.Short,
                SeoFocusKeyword = item.Name,
                SeoScore = 70
            });
            index++;
        }

        await db.SaveChangesAsync();
        await EnsureReviewsAsync(db);
        await EnsurePostsAsync(db, admin.Id, categories["tin-tuc"].Id);
        await EnsurePagesAsync(db);
        await EnsureMenusAsync(db);
        await EnsureSettingsAsync(db);

        db.SystemSettings.Add(new SystemSetting
        {
            Key = "DemoCatalog",
            Value = "2",
            Description = "Đã nạp dữ liệu mẫu cửa hàng gốm"
        });
        await db.SaveChangesAsync();
    }

    private static async Task AlignImagesAsync(AppDbContext db)
    {
        var images = Products.ToDictionary(item => item.Slug, item => item.Image);
        var slugs = images.Keys.ToList();
        var rows = await db.Products.Where(item => slugs.Contains(item.Slug)).ToListAsync();
        foreach (var row in rows)
            row.ImageUrl = images[row.Slug];
    }

    private static async Task HidePlaceholdersAsync(AppDbContext db)
    {
        var products = await db.Products
            .Where(item => item.Slug.StartsWith("san-pham-") || item.Name.StartsWith("sản phẩm"))
            .ToListAsync();
        foreach (var item in products)
            item.IsVisible = false;

        var posts = await db.Posts
            .Where(item => item.Slug.StartsWith("bai-viet") || item.Title.StartsWith("bài viết"))
            .ToListAsync();
        foreach (var item in posts)
            item.IsPublished = false;
    }

    private static async Task RenamePlaceholderCategoryAsync(AppDbContext db, string fromSlug, string name, string toSlug)
    {
        if (await db.Categories.AnyAsync(item => item.Slug == toSlug))
            return;

        var category = await db.Categories.FirstOrDefaultAsync(item => item.Slug == fromSlug);
        if (category is null)
            return;

        category.Name = name;
        category.Slug = toSlug;
        category.Type = "Product";
        await db.SaveChangesAsync();
    }

    private static async Task<Category> EnsureCategoryAsync(AppDbContext db, string name, string slug, string type)
    {
        var category = await db.Categories.FirstOrDefaultAsync(item => item.Slug == slug);
        if (category is not null)
            return category;

        category = new Category { Name = name, Slug = slug, Type = type };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static async Task EnsureReviewsAsync(AppDbContext db)
    {
        var comments = new (string Slug, string Name, int Rating, string Comment)[]
        {
            ("am-chen-men-ran", "Lan Anh", 5, "Men rạn đều, nắp khít, rót trà không dây."),
            ("am-chen-men-ngoc", "Minh Khoa", 4, "Dáng thấp chắc tay, màu men dịu."),
            ("khay-tra-go", "Hồng Nhung", 5, "Mặt khay thoát nước tốt, gỗ thơm nhẹ.")
        };

        foreach (var item in comments)
        {
            var product = await db.Products.FirstOrDefaultAsync(product => product.Slug == item.Slug);
            if (product is null || await db.ProductReviews.AnyAsync(review => review.ProductId == product.Id && review.CustomerName == item.Name))
                continue;

            db.ProductReviews.Add(new ProductReview
            {
                ProductId = product.Id,
                CustomerName = item.Name,
                Rating = item.Rating,
                Comment = item.Comment,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static async Task EnsurePostsAsync(AppDbContext db, int authorId, int categoryId)
    {
        var posts = new (string Title, string Slug, string Summary, string Image)[]
        {
            ("Cách chọn bộ ấm chén cho bàn trà nhỏ", "chon-bo-am-chen", "Gợi ý dung tích, dáng ấm và màu men khi bàn trà chỉ rộng một mét.", Img[0]),
            ("Pha trà sen trong ấm men rạn", "pha-tra-sen", "Nước sôi già làm nở men rạn. Nên tráng ấm trước khi cho trà.", Img[1]),
            ("Bảo quản khay gỗ sau mỗi tuần trà", "bao-quan-khay-go", "Lau khô mặt khay, tránh ngâm nước và kê nơi thoáng.", Img[1])
        };

        foreach (var item in posts)
        {
            if (await db.Posts.AnyAsync(post => post.Slug == item.Slug))
                continue;

            db.Posts.Add(new Post
            {
                CategoryId = categoryId,
                AuthorId = authorId,
                Title = item.Title,
                Slug = item.Slug,
                Summary = item.Summary,
                Content = $"<p>{item.Summary}</p><p>Đây là bài viết mẫu để xem bố cục trang tin. Nội dung có thể sửa trong phần quản trị.</p>",
                ImageUrl = item.Image,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                MetaTitle = item.Title,
                MetaDescription = item.Summary,
                SeoScore = 60
            });
        }
    }

    private static async Task EnsurePagesAsync(AppDbContext db)
    {
        var pages = new (string Title, string Slug, string Content)[]
        {
            ("Giới thiệu", "gioi-thieu", "<p>Gốm An Khang là cửa hàng mẫu, chuyên ấm chén, khay trà và đồ phụ kiện cho bàn trà gia đình.</p><p>Đơn hàng trên website này dùng để thử giao diện đặt hàng.</p>"),
            ("Chính sách giao hàng", "chinh-sach-giao-hang", "<p>Đơn trong nội thành giao trong 2 ngày. Đơn ngoại thành giao trong 4 ngày làm việc.</p>"),
            ("Chính sách đổi trả", "chinh-sach-doi-tra", "<p>Sản phẩm nứt vỡ do vận chuyển được đổi trong 3 ngày sau khi nhận, nếu còn nguyên hộp.</p>")
        };

        foreach (var item in pages)
        {
            if (await db.CustomPages.AnyAsync(page => page.Slug == item.Slug))
                continue;

            db.CustomPages.Add(new CustomPage
            {
                Title = item.Title,
                Slug = item.Slug,
                Content = item.Content,
                IsPublished = true,
                MetaTitle = item.Title,
                SeoScore = 50
            });
        }
    }

    private static async Task EnsureMenusAsync(AppDbContext db)
    {
        var productMenu = await db.Menus.FirstOrDefaultAsync(menu => menu.Position == "Header" && (menu.Url == "/san-pham" || menu.Url == "san-pham"));
        if (productMenu is null)
        {
            productMenu = new Menu { Name = "Sản phẩm", Url = "/san-pham", Position = "Header", DisplayOrder = 3, IsActive = true };
            db.Menus.Add(productMenu);
            await db.SaveChangesAsync();
        }

        var children = new (string Name, string Url, int Order)[]
        {
            ("Bộ ấm chén", "/danh-muc/bo-am-chen", 1),
            ("Khay trà", "/danh-muc/khay-tra", 2),
            ("Phụ kiện bàn trà", "/danh-muc/phu-kien-ban-tra", 3),
            ("Lư xông trầm", "/danh-muc/lu-xong-tram", 4),
            ("Hũ trà", "/danh-muc/hu-tra", 5)
        };
        foreach (var item in children)
        {
            if (await db.Menus.AnyAsync(menu => menu.Url == item.Url))
                continue;

            db.Menus.Add(new Menu
            {
                Name = item.Name,
                Url = item.Url,
                ParentId = productMenu.Id,
                Position = "Header",
                DisplayOrder = item.Order,
                IsActive = true
            });
        }

        await EnsureMenuAsync(db, "Giới thiệu", "/gioi-thieu", "Header", 2);
        await EnsureMenuAsync(db, "Tin tức", "/blog", "Header", 4);
        await EnsureMenuAsync(db, "Giới thiệu", "/gioi-thieu", "Footer", 1);
        await EnsureMenuAsync(db, "Giao hàng", "/chinh-sach-giao-hang", "Footer", 2);
        await EnsureMenuAsync(db, "Đổi trả", "/chinh-sach-doi-tra", "Footer", 3);
    }

    private static async Task EnsureMenuAsync(AppDbContext db, string name, string url, string position, int order)
    {
        if (await db.Menus.AnyAsync(menu => menu.Position == position && menu.Url == url))
            return;

        db.Menus.Add(new Menu
        {
            Name = name,
            Url = url,
            Position = position,
            DisplayOrder = order,
            IsActive = true
        });
    }

    private static async Task EnsureSettingsAsync(AppDbContext db)
    {
        await UpsertSettingAsync(db, "SiteName", "Gốm An Khang", "Tên cửa hàng", current => string.IsNullOrWhiteSpace(current) || current == "WebShop");
        await UpsertSettingAsync(db, "Hotline", "0901 234 567", "Số điện thoại", string.IsNullOrWhiteSpace);
        await UpsertSettingAsync(db, "Address", "18 Hàng Bạc, Hoàn Kiếm, Hà Nội", "Địa chỉ", string.IsNullOrWhiteSpace);
    }

    private static async Task UpsertSettingAsync(AppDbContext db, string key, string value, string description, Func<string?, bool> replace)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == key);
        if (setting is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
            return;
        }

        if (replace(setting.Value))
            setting.Value = value;
    }

    private static readonly string[] Img =
    [
        "https://images.unsplash.com/photo-1544787219-7f47ccb76574?auto=format&fit=crop&w=900&q=80",
        "https://images.unsplash.com/photo-1564890369478-c89ca6d9cde9?auto=format&fit=crop&w=900&q=80",
        "https://images.unsplash.com/photo-1556679343-c7306c1976bc?auto=format&fit=crop&w=900&q=80",
        "https://images.unsplash.com/photo-1571934811356-5cc061b6821f?auto=format&fit=crop&w=900&q=80",
        "https://images.unsplash.com/photo-1597318181409-cf64d0b5d8a2?auto=format&fit=crop&w=900&q=80",
        "https://images.unsplash.com/photo-1515694346937-94d85e41e6f0?auto=format&fit=crop&w=900&q=80",
        "https://images.unsplash.com/photo-1470337458703-46ad1756a187?auto=format&fit=crop&w=900&q=80",
        "https://images.unsplash.com/photo-1523906834658-6e24ef2386f9?auto=format&fit=crop&w=900&q=80"
    ];

    private static readonly DemoProduct[] Products =
    [
        new("Bộ ấm chén men rạn dáng cao", "am-chen-men-ran", "bo-am-chen", 1850000, 1450000, 900000, 8, "Ấm 200ml, sáu chén tống, men rạn ngà.", "Phù hợp bàn trà tiếp khách. Nắp khít, quai ấm vừa tay.", Img[0]),
        new("Bộ ấm chén men ngọc dáng thấp", "am-chen-men-ngoc", "bo-am-chen", 1280000, null, 700000, 10, "Men ngọc dày, dáng thấp, khó đổ.", "Bộ bốn chén, khay lót gốm cùng men.", Img[1]),
        new("Bộ ấm chén quả hồng men nâu", "am-chen-qua-hong", "bo-am-chen", 2150000, 1680000, 1100000, 5, "Dáng quả hồng, men nâu bóng.", "Ấm dung tích lớn hơn, hợp trà sen và trà shan.", Img[2]),
        new("Ấm tích men lam cổ", "am-tich-men-lam", "bo-am-chen", 980000, null, 520000, 7, "Họa tiết lam nhẹ, miệng loe.", "Dùng riêng hoặc kèm chén tống men trắng.", Img[3]),
        new("Khay trà gỗ mun mặt lõm", "khay-tra", "khay-tra", 890000, null, 480000, 6, "Mặt khay lõm, thoát nước về một góc.", "Gỗ mun để mộc, rộng 48cm.", Img[1]),
        new("Khay trà tre đan viền thấp", "khay-tre", "khay-tra", 420000, 350000, 180000, 14, "Tre đan, nhẹ, dễ mang đi.", "Lót có thể tháo để rửa.", Img[3]),
        new("Chén tống men hỏa biến", "chen-tong-hoa-bien", "phu-kien-ban-tra", 180000, null, 70000, 30, "Mỗi chén một vệt men khác nhau.", "Bán lẻ, đường kính miệng 7cm.", Img[0]),
        new("Bộ phụ kiện sáu món", "phu-kien-sau-mon", "phu-kien-ban-tra", 310000, null, 140000, 12, "Gắp, kim, phễu, thìa, lọ tăm, kẹp.", "Kim loại màu đồng giả cổ.", Img[1]),
        new("Lư xông trầm dáng tròn", "lu-xong-tram", "lu-xong-tram", 650000, 520000, 280000, 9, "Nắp đục lỗ, đế rộng.", "Đặt cuối bàn trà, dùng nụ trầm nhỏ.", Img[2]),
        new("Hũ trà men celadon", "hu-tra-celadon", "hu-tra", 390000, null, 160000, 16, "Nắp kín, men xanh ngọc.", "Đựng khoảng 150g trà khô.", Img[3]),
        new("Đĩa kê ấm men nâu", "dia-ke-am", "phu-kien-ban-tra", 150000, null, 60000, 20, "Đĩa tròn hứng nước ấm.", "Đường kính 12cm.", Img[0]),
        new("Hũ trà men trắng nắp gỗ", "hu-tra-nap-go", "hu-tra", 270000, 220000, 110000, 18, "Thân gốm trắng, nắp gỗ.", "Phù hợp trà ô long và trà sen.", Img[1])
    ];

    private sealed record DemoProduct(
        string Name,
        string Slug,
        string CategorySlug,
        decimal Price,
        decimal? Discount,
        decimal Cost,
        int Stock,
        string Short,
        string Detail,
        string Image);
}
