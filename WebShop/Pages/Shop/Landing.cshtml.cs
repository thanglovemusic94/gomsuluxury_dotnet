using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class LandingModel(AppDbContext db, ShopStore shop) : PageModel
{
    public LandingPage PageData { get; private set; } = null!;

    public IReadOnlyList<OfferProduct> Offers { get; private set; } = [];

    public string? OrderCode { get; private set; }

    public decimal? OrderTotal { get; private set; }

    public bool ShowExpired { get; private set; }

    [BindProperty]
    public LeadInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        OrderCode = TempData["OrderCode"] as string;
        if (TempData["OrderTotal"] is decimal total)
            OrderTotal = total;
        else if (TempData["OrderTotal"] is double totalD)
            OrderTotal = (decimal)totalD;

        var page = await LoadPageAsync(slug);
        if (page is null)
            return NotFound();

        if (string.IsNullOrEmpty(OrderCode) && IsOfferOver(page))
            return await HandleInactiveAsync(page);

        if (!page.IsActive && string.IsNullOrEmpty(OrderCode))
            return await HandleInactiveAsync(page);

        await BindPageAsync(page);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        var page = await LoadPageAsync(slug);
        if (page is null)
            return NotFound();
        if (!page.IsActive || IsOfferOver(page))
            return await HandleInactiveAsync(page);

        await BindPageAsync(page);

        Input.CustomerName = TextHelper.Trimmed(Input.CustomerName);
        Input.CustomerPhone = TextHelper.Trimmed(Input.CustomerPhone);
        Input.ShippingAddress = TextHelper.Trimmed(Input.ShippingAddress);
        Input.OrderNote = TextHelper.Clean(Input.OrderNote);

        if (Input.CustomerName.Length is < 2 or > 150)
            ModelState.AddModelError(string.Empty, "Nhập tên người nhận.");
        if (Input.CustomerPhone.Length is < 8 or > 20)
            ModelState.AddModelError(string.Empty, "Nhập số điện thoại.");
        if (Input.ShippingAddress.Length is < 5 or > 500)
            ModelState.AddModelError(string.Empty, "Nhập địa chỉ giao hàng.");

        var offer = Offers.FirstOrDefault(item => item.Product.Id == Input.ProductId);
        if (offer is null)
            ModelState.AddModelError(string.Empty, "Chọn sản phẩm.");
        else if (offer.Product.Stock < 1)
            ModelState.AddModelError(string.Empty, $"Hết hàng: {offer.Product.Name}.");
        else if (Input.Quantity < 1 || Input.Quantity > offer.Product.Stock)
            ModelState.AddModelError(string.Empty, "Số lượng không hợp lệ.");

        if (!ModelState.IsValid || offer is null)
            return Page();

        var tracked = await db.Products.FirstAsync(item => item.Id == offer.Product.Id);
        if (tracked.Stock < Input.Quantity)
        {
            ModelState.AddModelError(string.Empty, $"Không đủ tồn kho: {tracked.Name}.");
            return Page();
        }

        var sequence = await db.Orders.CountAsync() + 1;
        var code = $"DH-{DateTime.UtcNow.Year}-{sequence:000}";
        while (await db.Orders.AnyAsync(order => order.OrderCode == code))
        {
            sequence++;
            code = $"DH-{DateTime.UtcNow.Year}-{sequence:000}";
        }

        var unitPrice = offer.UnitPrice;
        var order = new Order
        {
            OrderCode = code,
            Status = "Pending",
            PaymentStatus = "Unpaid",
            PaymentMethod = "COD",
            CustomerName = Input.CustomerName,
            CustomerPhone = Input.CustomerPhone,
            ShippingAddress = Input.ShippingAddress,
            OrderNote = Input.OrderNote,
            Source = "LP:" + page.Slug,
            TotalAmount = unitPrice * Input.Quantity
        };
        order.Items.Add(new OrderItem
        {
            ProductId = tracked.Id,
            Quantity = Input.Quantity,
            UnitPrice = unitPrice,
            OriginalCostPrice = tracked.CostPrice
        });
        tracked.Stock -= Input.Quantity;

        db.Orders.Add(order);
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["OrderCode"] = order.OrderCode;
        TempData["OrderTotal"] = order.TotalAmount;
        return RedirectToPage(new { slug });
    }

    private async Task<LandingPage?> LoadPageAsync(string slug)
    {
        var clean = TextHelper.Slug(slug, slug);
        if (string.IsNullOrEmpty(clean))
            return null;

        return await db.LandingPages.AsNoTracking()
            .Include(item => item.Products)
            .ThenInclude(link => link.Product)
            .FirstOrDefaultAsync(item => item.Slug == clean);
    }

    private async Task BindPageAsync(LandingPage page)
    {
        PageData = page;
        Offers = page.Products
            .OrderBy(link => link.DisplayOrder)
            .Where(link => link.Product is not null && link.Product.IsVisible && !link.Product.IsDeleted)
            .Select(link => OfferProduct.From(link))
            .ToList();

        if (Input.ProductId == 0 && Offers.Count > 0)
            Input.ProductId = Offers[0].Product.Id;
        if (Input.Quantity < 1)
            Input.Quantity = 1;

        var snap = await shop.GetAsync();
        ViewData["Title"] = page.MetaTitle ?? page.Headline;
        ViewData["Description"] = page.MetaDescription ?? page.Subheadline;
        ViewData["AdsSiteName"] = snap.SiteName;
        ViewData["AdsLogoUrl"] = snap.LogoUrl;
        ViewData["AdsHotline"] = snap.Hotline;
        ViewData["AdsHotlineHref"] = string.IsNullOrWhiteSpace(snap.Hotline)
            ? null
            : "tel:" + new string(snap.Hotline.Where(char.IsDigit).ToArray());
        ViewData["LpHeadScripts"] = page.HeadScripts;
        ViewData["LpBodyScripts"] = page.BodyScripts;
        ViewData["LpMetaPixelId"] = page.MetaPixelId;
        ViewData["LpTikTokPixelId"] = page.TikTokPixelId;
        if (!string.IsNullOrEmpty(OrderCode))
        {
            ViewData["LpConversion"] = true;
            ViewData["LpOrderCode"] = OrderCode;
            ViewData["LpOrderTotal"] = OrderTotal ?? 0m;
        }

        var hero = SiteSocial.PickImage(page.SeoImage, page.HeroImageUrl);
        if (!string.IsNullOrWhiteSpace(hero))
            ViewData["LcpImage"] = MediaUrls.For(hero, MediaSize.Large);
    }

    private async Task<IActionResult> HandleInactiveAsync(LandingPage page)
    {
        var mode = page.RedirectWhenOff?.Trim() ?? "Home";
        if (string.Equals(mode, "Product", StringComparison.OrdinalIgnoreCase))
        {
            var first = page.Products.OrderBy(item => item.DisplayOrder).Select(item => item.Product).FirstOrDefault();
            if (first is not null && first.IsVisible && !first.IsDeleted)
                return Redirect("/san-pham/" + first.Slug);
            return Redirect("/");
        }

        if (string.Equals(mode, "Expired", StringComparison.OrdinalIgnoreCase))
        {
            ShowExpired = true;
            await BindPageAsync(page);
            return Page();
        }

        return Redirect("/");
    }

    private static bool IsOfferOver(LandingPage page) =>
        page.OfferEndsAt is DateTime ends && ends <= DateTime.Now;

    public class LeadInput
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string? OrderNote { get; set; }
    }

    public sealed class OfferProduct
    {
        public required Product Product { get; init; }
        public decimal ListPrice { get; init; }
        public decimal UnitPrice { get; init; }
        public int? DiscountPercent { get; init; }

        public static OfferProduct From(LandingPageProduct link)
        {
            var product = link.Product;
            var list = product.Price;
            var catalogSale = product.DiscountPrice ?? product.Price;
            var unit = link.AdsPrice is > 0 ? link.AdsPrice.Value : catalogSale;
            int? pct = null;
            if (list > 0 && unit < list)
                pct = (int)Math.Round((1 - unit / list) * 100m);
            return new OfferProduct
            {
                Product = product,
                ListPrice = list,
                UnitPrice = unit,
                DiscountPercent = pct is > 0 ? pct : null
            };
        }
    }
}
