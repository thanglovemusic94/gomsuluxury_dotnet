using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages;

public class IndexModel(AppDbContext db, ShopStore shop) : PageModel
{
    public string SiteName { get; private set; } = "WebShop";

    public string Hotline { get; private set; } = string.Empty;

    public IReadOnlyList<ShopSlide> Slides { get; private set; } = ShopSlides.Default;

    public IReadOnlyList<CategoryNavItem> Categories { get; private set; } = [];

    public IReadOnlyList<Product> Products { get; private set; } = [];

    public IReadOnlyList<Post> Posts { get; private set; } = [];

    public IReadOnlyList<Product> GiftProducts { get; private set; } = [];

    public string? GiftTeaserImage { get; private set; }

    public async Task OnGetAsync()
    {
        var snap = await shop.GetAsync();
        SiteName = snap.SiteName;
        Hotline = snap.Hotline;
        Categories = snap.CategoryNav;
        Slides = await ShopSlides.LoadAsync(db);
        if (Slides.Count > 0)
        {
            // LCP: Medium..Large (Max) — không Thumb crop vuông.
            ViewData["LcpImage"] = MediaUrls.For(Slides[0].ImageUrl, MediaSize.Medium);
            ViewData["LcpImageSrcSet"] = MediaUrls.SrcSetBetween(Slides[0].ImageUrl, MediaSize.Medium, MediaSize.Large);
            ViewData["LcpImageSizes"] = "(max-width: 991px) 100vw, min(920px, calc(100vw - 280px))";
            ViewData["OgImage"] = Slides[0].ImageUrl;
        }

        ViewData["Description"] = SiteName + " — gốm sứ cao cấp, quà biếu và bộ ấm trà.";
        ViewData["OgType"] = "website";
        ViewData["ShopAssets"] = "home";

        Products = await db.Products.AsNoTracking()
            .Where(item => item.IsVisible)
            .OrderByDescending(item => item.CreatedAt)
            .Take(8)
            .ToListAsync();

        GiftProducts = await LoadGiftProductsAsync();
        if (GiftProducts.Count > 0)
            GiftTeaserImage = MediaDynamicUrls.For(GiftProducts[0].ImageUrl, 720);

        Posts = await db.Posts.AsNoTracking()
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.CreatedAt)
            .Take(3)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<Product>> LoadGiftProductsAsync()
    {
        var giftCatId = await db.Categories.AsNoTracking()
            .Where(item => item.Type == "Product" && item.Slug == GiftLandingSeed.CategorySlug)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync();

        if (giftCatId is int id)
        {
            var fromCat = await db.Products.AsNoTracking()
                .Where(item => item.IsVisible && item.ProductCategories.Any(link => link.CategoryId == id))
                .OrderByDescending(item => item.CreatedAt)
                .Take(4)
                .ToListAsync();
            if (fromCat.Count > 0)
                return fromCat;
        }

        var slugs = GiftLandingSeed.FallbackCategorySlugs;
        var fallback = await db.Products.AsNoTracking()
            .Where(item => item.IsVisible && item.ProductCategories.Any(link => slugs.Contains(link.Category.Slug)))
            .OrderByDescending(item => item.CreatedAt)
            .Take(4)
            .ToListAsync();
        if (fallback.Count > 0)
            return fallback;

        return Products.Take(4).ToList();
    }
}
