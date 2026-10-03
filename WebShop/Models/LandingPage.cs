namespace WebShop.Models;

public class LandingPage
{
    public int Id { get; set; }

    /// <summary>Tên chiến dịch (Admin).</summary>
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Home | Product | Expired</summary>
    public string RedirectWhenOff { get; set; } = "Home";

    public string Headline { get; set; } = string.Empty;

    public string? Subheadline { get; set; }

    public string? HeroImageUrl { get; set; }

    public string CtaText { get; set; } = "Đặt mua ngay";

    /// <summary>Để trống = cuộn tới form đặt hàng.</summary>
    public string? CtaUrl { get; set; }

    public string? BodyHtml { get; set; }

    public string ThankYouMessage { get; set; } =
        "Cảm ơn bạn! Chúng tôi sẽ liên hệ xác nhận đơn trong thời gian sớm nhất.";

    /// <summary>Hết ưu đãi (giờ máy chủ). Null = không đếm ngược.</summary>
    public DateTime? OfferEndsAt { get; set; }

    /// <summary>Meta Pixel ID (chỉ số). Tự gắn snippet chuẩn nếu có.</summary>
    public string? MetaPixelId { get; set; }

    /// <summary>TikTok Pixel ID. Tự gắn snippet chuẩn nếu có.</summary>
    public string? TikTokPixelId { get; set; }

    /// <summary>HTML/JS tùy chỉnh trong &lt;head&gt; (Admin trusted).</summary>
    public string? HeadScripts { get; set; }

    /// <summary>HTML/JS trước &lt;/body&gt; (Admin trusted).</summary>
    public string? BodyScripts { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? SeoImage { get; set; }

    public string? SeoFocusKeyword { get; set; }

    public int SeoScore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<LandingPageProduct> Products { get; set; } = [];
}

public class LandingPageProduct
{
    public int LandingPageId { get; set; }

    public int ProductId { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Giá flash Ads; null = dùng DiscountPrice/Price catalog.</summary>
    public decimal? AdsPrice { get; set; }

    public LandingPage LandingPage { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
