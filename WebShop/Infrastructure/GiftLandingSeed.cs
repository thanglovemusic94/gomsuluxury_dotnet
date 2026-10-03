using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Danh mục Quà biếu + menu + HtmlBlock (chỉ tạo lần đầu — Admin sửa thoải mái).
/// Không chuyển sản phẩm khỏi danh mục gốc.
/// </summary>
public static class GiftLandingSeed
{
    public const string CategorySlug = "qua-bieu";
    public const string PagePath = "/qua-bieu";
    public const string HomeBlockKey = "home-gift";
    public const string HeroBlockKey = "gift-hero";
    public const string OccasionsBlockKey = "gift-occasions";
    public const string LeadBlockKey = "gift-lead";
    public const string CtaBlockKey = "gift-cta";

    /// <summary>Slug danh mục dùng gợi ý khi Quà biếu chưa có SP.</summary>
    public static readonly string[] FallbackCategorySlugs =
    [
        "khay-tra",
        "phu-kien-ban-tra",
        "hu-tra",
        "tra-shan-tuyet",
        "tuong-gom-linh-vat",
        "lu-xong-tram"
    ];

    public static async Task EnsureAsync(AppDbContext db)
    {
        await EnsureCategoryAsync(db);
        await EnsureMenuAsync(db);

        await EnsureBlockOnceAsync(db, HomeBlockKey, "Teaser Quà biếu (trang chủ)",
            """
            <h2 id="home-gift-heading" class="shop-gift-teaser__title">Quà biếu</h2>
            <p>Set gốm, khay trà và món biếu sẵn sàng gửi trao — chọn nhanh trên web hoặc chốt qua livestream.</p>
            """,
            "Tiêu đề + mô tả block Quà biếu trên trang chủ. Giữ class shop-gift-teaser__title. Tắt IsActive để ẩn chữ.");

        await EnsureBlockOnceAsync(db, HeroBlockKey, "Hero trang Quà biếu",
            """
            <h1 class="shop-gift-hero__title">Quà biếu tinh tế</h1>
            <p class="shop-gift-hero__lead">Set gốm &amp; bàn trà sẵn sàng gửi trao — chọn trên web hoặc chốt nhanh qua livestream.</p>
            """,
            "Tiêu đề + mô tả hero /qua-bieu. Giữ class shop-gift-hero__title và shop-gift-hero__lead. Brand lấy từ Settings.");

        await EnsureBlockOnceAsync(db, OccasionsBlockKey, "Gợi ý theo dịp (Quà biếu)",
            """
            <h2 id="gift-occasions-heading" class="shop-gift-occasions__title">Gợi ý theo dịp</h2>
            <p class="shop-gift-occasions__lead">Chọn hướng biếu — chúng tôi tư vấn set phù hợp ngân sách.</p>
            <ul class="shop-gift-occasions__list">
            	<li><a href="#bo-qua">Tết &amp; năm mới</a></li>
            	<li><a href="#bo-qua">Biếu sếp / đối tác</a></li>
            	<li><a href="#bo-qua">Tân gia &amp; khai trương</a></li>
            </ul>
            """,
            "Khối gợi ý theo dịp trên /qua-bieu. Giữ class shop-gift-occasions__*.");

        await EnsureBlockOnceAsync(db, LeadBlockKey, "Ghi chú trang Quà biếu",
            """
            <p>Đóng gói kỹ, hỗ trợ gửi biếu theo địa chỉ người nhận. Liên hệ hotline để tư vấn set theo ngân sách.</p>
            """,
            "Hiển thị dưới lưới sản phẩm trên /qua-bieu.");

        await EnsureBlockOnceAsync(db, CtaBlockKey, "CTA cuối trang Quà biếu",
            """
            <h2 class="shop-gift-band__title">Cần set theo ngân sách?</h2>
            <p class="shop-gift-band__text">Nhắn hotline hoặc chốt mã trên livestream — chúng tôi gửi link hoàn tất giao hàng.</p>
            """,
            "Chữ CTA cuối /qua-bieu (nút hotline vẫn lấy từ Settings). Giữ class shop-gift-band__title và shop-gift-band__text.");

        await UpgradeHomeGiftTitleOnceAsync(db);
        await db.SaveChangesAsync();
    }

    /// <summary>Block home-gift cũ chỉ có &lt;p&gt; — bổ sung tiêu đề một lần.</summary>
    private static async Task UpgradeHomeGiftTitleOnceAsync(AppDbContext db)
    {
        const string marker = "GiftLanding.HomeGiftTitle";
        if (await db.SystemSettings.AnyAsync(item => item.Key == marker && item.Value == "1"))
            return;

        var block = await db.HtmlBlocks.FirstOrDefaultAsync(item => item.Key == HomeBlockKey);
        if (block is not null &&
            block.Content.IndexOf("shop-gift-teaser__title", StringComparison.OrdinalIgnoreCase) < 0)
        {
            block.Content =
                """
                <h2 id="home-gift-heading" class="shop-gift-teaser__title">Quà biếu</h2>
                """ + "\n" + block.Content.Trim();
            block.Note = "Tiêu đề + mô tả block Quà biếu trên trang chủ. Giữ class shop-gift-teaser__title.";
            block.UpdatedAt = DateTime.UtcNow;
        }

        db.SystemSettings.Add(new SystemSetting
        {
            Key = marker,
            Value = "1",
            Description = "Đã gắn tiêu đề vào HtmlBlock home-gift"
        });
    }

    private static async Task EnsureCategoryAsync(AppDbContext db)
    {
        var category = await db.Categories.FirstOrDefaultAsync(item => item.Slug == CategorySlug);
        if (category is not null)
        {
            if (category.Type != "Product")
                category.Type = "Product";
            if (!string.Equals(category.Name, "Quà biếu", StringComparison.Ordinal))
                category.Name = "Quà biếu";
            return;
        }

        db.Categories.Add(new Category
        {
            Name = "Quà biếu",
            Slug = CategorySlug,
            Type = "Product"
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureMenuAsync(AppDbContext db)
    {
        if (await db.Menus.AnyAsync(menu =>
                menu.Position == "Header" &&
                (menu.Url == PagePath || menu.Url == "/danh-muc/" + CategorySlug)))
            return;

        db.Menus.Add(new Menu
        {
            Name = "Quà biếu",
            Url = PagePath,
            Position = "Header",
            DisplayOrder = 4,
            IsActive = true
        });
    }

    private static async Task EnsureBlockOnceAsync(AppDbContext db, string key, string title, string content, string note)
    {
        if (await db.HtmlBlocks.AnyAsync(block => block.Key == key))
            return;

        db.HtmlBlocks.Add(new HtmlBlock
        {
            Key = key,
            Title = title,
            Content = content.Trim(),
            Note = note,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
