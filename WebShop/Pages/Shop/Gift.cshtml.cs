using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class GiftModel(AppDbContext db, ShopStore shop) : PageModel
{
    /// <summary>Route segment: /qua-bieu/ads</summary>
    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    /// <summary>Query: /qua-bieu?ads=1</summary>
    [BindProperty(SupportsGet = true)]
    public bool Ads { get; set; }

    public bool IsAdsLanding { get; private set; }

    public string SiteName { get; private set; } = "WebShop";

    public string LogoUrl { get; private set; } = string.Empty;

    public string Hotline { get; private set; } = string.Empty;

    public string? HotlineHref { get; private set; }

    public string? HeroImageUrl { get; private set; }

    public bool FromGiftCategory { get; private set; }

    public IReadOnlyList<Product> Products { get; private set; } = [];

    public async Task OnGetAsync()
    {
        IsAdsLanding = Ads ||
            string.Equals(Mode, "ads", StringComparison.OrdinalIgnoreCase);

        var snap = await shop.GetAsync();
        SiteName = snap.SiteName;
        LogoUrl = snap.LogoUrl;
        Hotline = snap.Hotline;
        HotlineHref = string.IsNullOrWhiteSpace(Hotline)
            ? null
            : "tel:" + Hotline.Replace(" ", "", StringComparison.Ordinal);

        var giftCat = await db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Type == "Product" && item.Slug == GiftLandingSeed.CategorySlug);

        List<Product> products = [];
        if (giftCat is not null)
        {
            products = await db.Products.AsNoTracking()
                .Where(item => item.IsVisible && item.ProductCategories.Any(link => link.CategoryId == giftCat.Id))
                .OrderByDescending(item => item.CreatedAt)
                .Take(12)
                .ToListAsync();
            FromGiftCategory = products.Count > 0;
        }

        if (products.Count == 0)
        {
            var slugs = GiftLandingSeed.FallbackCategorySlugs;
            products = await db.Products.AsNoTracking()
                .Where(item => item.IsVisible && item.ProductCategories.Any(link => slugs.Contains(link.Category.Slug)))
                .OrderByDescending(item => item.CreatedAt)
                .Take(8)
                .ToListAsync();
        }

        if (products.Count == 0)
        {
            products = await db.Products.AsNoTracking()
                .Where(item => item.IsVisible)
                .OrderByDescending(item => item.CreatedAt)
                .Take(8)
                .ToListAsync();
        }

        Products = products;
        if (products.Count > 0 && !string.IsNullOrWhiteSpace(products[0].ImageUrl))
        {
            HeroImageUrl = MediaUrls.For(products[0].ImageUrl, MediaSize.Large);
            ViewData["LcpImage"] = HeroImageUrl;
        }

        ViewData["Title"] = "Quà biếu | " + SiteName;
        ViewData["Description"] = "Set gốm sứ, khay trà và món biếu tinh tế — chọn quà gửi trao hoặc tư vấn qua hotline.";
        ViewData["ShopAssets"] = IsAdsLanding ? "" : "home";

        if (IsAdsLanding)
        {
            ViewData["AdsSiteName"] = SiteName;
            ViewData["AdsLogoUrl"] = LogoUrl;
            ViewData["AdsHotline"] = Hotline;
            ViewData["AdsHotlineHref"] = HotlineHref;
        }
    }
}
