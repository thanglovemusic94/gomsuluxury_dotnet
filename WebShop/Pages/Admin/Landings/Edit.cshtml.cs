using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Landings;

public class EditModel(AppDbContext db) : PageModel
{
    private static readonly string[] RedirectOptions = ["Home", "Product", "Expired"];

    [BindProperty]
    public LandingInput Input { get; set; } = new();

    [BindProperty]
    public SeoInput Seo { get; set; } = new();

    /// <summary>Giá Ads theo ProductId (chuỗi form, chỉ áp dụng khi SP được chọn).</summary>
    [BindProperty]
    public Dictionary<int, string?> AdsPrices { get; set; } = new();

    [BindProperty]
    public List<int> ProductIds { get; set; } = [];

    public IList<Product> Catalog { get; private set; } = [];

    public IReadOnlyList<string> RedirectWhenOffOptions => RedirectOptions;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadCatalogAsync();
        if (id is null)
            return Page();

        var page = await db.LandingPages.AsNoTracking()
            .Include(item => item.Products)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (page is null)
            return NotFound();

        Input = LandingInput.From(page);
        Seo = new SeoInput
        {
            MetaTitle = page.MetaTitle,
            MetaDescription = page.MetaDescription,
            SeoImage = page.SeoImage,
            SeoFocusKeyword = page.SeoFocusKeyword,
            SeoScore = page.SeoScore
        };
        ProductIds = page.Products.OrderBy(item => item.DisplayOrder).Select(item => item.ProductId).ToList();
        AdsPrices = page.Products.ToDictionary(
            item => item.ProductId,
            item => item.AdsPrice is null ? null : ((int)item.AdsPrice.Value).ToString(CultureInfo.InvariantCulture));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCatalogAsync();
        Input.Name = TextHelper.Trimmed(Input.Name);
        Input.Headline = TextHelper.Trimmed(Input.Headline);
        Input.Subheadline = TextHelper.Clean(Input.Subheadline);
        Input.HeroImageUrl = TextHelper.Clean(Input.HeroImageUrl);
        Input.CtaText = string.IsNullOrWhiteSpace(Input.CtaText) ? "Đặt mua ngay" : TextHelper.Trimmed(Input.CtaText);
        Input.CtaUrl = TextHelper.Clean(Input.CtaUrl);
        Input.BodyHtml = TextHelper.Trimmed(Input.BodyHtml);
        Input.ThankYouMessage = string.IsNullOrWhiteSpace(Input.ThankYouMessage)
            ? "Cảm ơn bạn! Chúng tôi sẽ liên hệ xác nhận đơn trong thời gian sớm nhất."
            : TextHelper.Trimmed(Input.ThankYouMessage);
        Input.MetaPixelId = NormalizePixelId(Input.MetaPixelId);
        Input.TikTokPixelId = NormalizePixelId(Input.TikTokPixelId);
        Input.HeadScripts = TextHelper.Clean(Input.HeadScripts);
        Input.BodyScripts = TextHelper.Clean(Input.BodyScripts);
        if (!RedirectOptions.Contains(Input.RedirectWhenOff))
            Input.RedirectWhenOff = "Home";

        if (Input.Name.Length is < 2 or > 200)
            ModelState.AddModelError("Input.Name", "Tên chiến dịch từ 2 đến 200 ký tự.");
        if (Input.Headline.Length is < 2 or > 250)
            ModelState.AddModelError("Input.Headline", "Tiêu đề hero từ 2 đến 250 ký tự.");
        if (Input.HeadScripts is { Length: > 20000 })
            ModelState.AddModelError("Input.HeadScripts", "Script head tối đa 20.000 ký tự.");
        if (Input.BodyScripts is { Length: > 20000 })
            ModelState.AddModelError("Input.BodyScripts", "Script body tối đa 20.000 ký tự.");
        Seo.Validate(ModelState);

        var slug = TextHelper.Slug(Input.Slug, Input.Name);
        if (string.IsNullOrEmpty(slug) || slug.Length > 150)
            ModelState.AddModelError("Input.Slug", "Slug không hợp lệ.");
        if (await db.LandingPages.AnyAsync(item => item.Slug == slug && item.Id != Input.Id))
            ModelState.AddModelError("Input.Slug", "Slug đã tồn tại.");

        ProductIds = ProductIds.Distinct().Where(id => Catalog.Any(p => p.Id == id)).ToList();
        if (ProductIds.Count == 0)
            ModelState.AddModelError(string.Empty, "Chọn ít nhất một sản phẩm.");

        var parsedPrices = new Dictionary<int, decimal?>();
        foreach (var productId in ProductIds)
        {
            AdsPrices.TryGetValue(productId, out var raw);
            raw = TextHelper.Clean(raw);
            if (string.IsNullOrEmpty(raw))
            {
                parsedPrices[productId] = null;
                continue;
            }

            raw = raw.Replace(".", "").Replace(",", "").Trim();
            if (!decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var price) || price < 0)
            {
                ModelState.AddModelError(string.Empty, $"Giá Ads không hợp lệ cho sản phẩm #{productId}.");
                continue;
            }

            parsedPrices[productId] = price;
        }

        if (!ModelState.IsValid)
            return Page();

        LandingPage entity;
        if (Input.Id == 0)
        {
            entity = new LandingPage();
            db.LandingPages.Add(entity);
        }
        else
        {
            var existing = await db.LandingPages
                .Include(item => item.Products)
                .FirstOrDefaultAsync(item => item.Id == Input.Id);
            if (existing is null)
                return NotFound();
            entity = existing;
        }

        entity.Name = Input.Name;
        entity.Slug = slug;
        entity.IsActive = Input.IsActive;
        entity.RedirectWhenOff = Input.RedirectWhenOff;
        entity.Headline = Input.Headline;
        entity.Subheadline = Input.Subheadline;
        entity.HeroImageUrl = Input.HeroImageUrl;
        entity.CtaText = Input.CtaText;
        entity.CtaUrl = Input.CtaUrl;
        entity.BodyHtml = string.IsNullOrWhiteSpace(Input.BodyHtml) ? null : Input.BodyHtml;
        entity.ThankYouMessage = Input.ThankYouMessage;
        entity.OfferEndsAt = Input.OfferEndsAt;
        entity.MetaPixelId = Input.MetaPixelId;
        entity.TikTokPixelId = Input.TikTokPixelId;
        entity.HeadScripts = string.IsNullOrWhiteSpace(Input.HeadScripts) ? null : Input.HeadScripts;
        entity.BodyScripts = string.IsNullOrWhiteSpace(Input.BodyScripts) ? null : Input.BodyScripts;
        entity.MetaTitle = TextHelper.Clean(Seo.MetaTitle);
        entity.MetaDescription = TextHelper.Clean(Seo.MetaDescription);
        entity.SeoImage = TextHelper.Clean(Seo.SeoImage);
        entity.SeoFocusKeyword = TextHelper.Clean(Seo.SeoFocusKeyword);
        entity.SeoScore = SeoScoreCalculator.Compute(new SeoScoreCalculator.Request(
            Title: entity.Headline,
            Slug: entity.Slug,
            FocusKeyword: entity.SeoFocusKeyword,
            MetaTitle: entity.MetaTitle,
            MetaDescription: entity.MetaDescription,
            SeoImage: entity.SeoImage,
            ImageUrl: entity.HeroImageUrl,
            ShortText: entity.Subheadline,
            HtmlBody: entity.BodyHtml));

        db.LandingPageProducts.RemoveRange(entity.Products);
        entity.Products.Clear();
        for (var i = 0; i < ProductIds.Count; i++)
        {
            var productId = ProductIds[i];
            entity.Products.Add(new LandingPageProduct
            {
                ProductId = productId,
                DisplayOrder = i,
                AdsPrice = parsedPrices.GetValueOrDefault(productId)
            });
        }

        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["Message"] = "Đã lưu landing page.";
        return RedirectToPage("Edit", new { id = entity.Id });
    }

    private async Task LoadCatalogAsync()
    {
        Catalog = await db.Products.AsNoTracking()
            .Where(item => item.IsVisible)
            .OrderBy(item => item.Name)
            .Select(item => new Product
            {
                Id = item.Id,
                Name = item.Name,
                Slug = item.Slug,
                Stock = item.Stock,
                Price = item.Price,
                DiscountPrice = item.DiscountPrice
            })
            .ToListAsync();
    }

    private static string? NormalizePixelId(string? value)
    {
        value = TextHelper.Clean(value);
        if (string.IsNullOrEmpty(value))
            return null;
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    public class LandingInput
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string RedirectWhenOff { get; set; } = "Home";
        public string Headline { get; set; } = string.Empty;
        public string? Subheadline { get; set; }
        public string? HeroImageUrl { get; set; }
        public string CtaText { get; set; } = "Đặt mua ngay";
        public string? CtaUrl { get; set; }
        public string? BodyHtml { get; set; }
        public string ThankYouMessage { get; set; } =
            "Cảm ơn bạn! Chúng tôi sẽ liên hệ xác nhận đơn trong thời gian sớm nhất.";
        public DateTime? OfferEndsAt { get; set; }
        public string? MetaPixelId { get; set; }
        public string? TikTokPixelId { get; set; }
        public string? HeadScripts { get; set; }
        public string? BodyScripts { get; set; }

        public static LandingInput From(LandingPage page) => new()
        {
            Id = page.Id,
            Name = page.Name,
            Slug = page.Slug,
            IsActive = page.IsActive,
            RedirectWhenOff = page.RedirectWhenOff,
            Headline = page.Headline,
            Subheadline = page.Subheadline,
            HeroImageUrl = page.HeroImageUrl,
            CtaText = page.CtaText,
            CtaUrl = page.CtaUrl,
            BodyHtml = page.BodyHtml,
            ThankYouMessage = page.ThankYouMessage,
            OfferEndsAt = page.OfferEndsAt,
            MetaPixelId = page.MetaPixelId,
            TikTokPixelId = page.TikTokPixelId,
            HeadScripts = page.HeadScripts,
            BodyScripts = page.BodyScripts
        };
    }
}
