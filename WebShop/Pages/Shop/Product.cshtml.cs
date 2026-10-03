using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class ProductModel(AppDbContext db, CartService cart) : PageModel
{
    public Product? Product { get; private set; }

    public IReadOnlyList<ProductReview> Reviews { get; private set; } = [];

    public IReadOnlyList<Product> RelatedProducts { get; private set; } = [];

    public string RelatedCatalogUrl { get; private set; } = "/san-pham";

    [BindProperty]
    public ReviewInput Review { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug) => await LoadAsync(slug) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAddAsync(string slug, int quantity)
    {
        var product = await Visible(slug);
        if (product is null)
            return NotFound();

        quantity = Math.Clamp(quantity, 1, Math.Max(product.Stock, 1));
        if (product.Stock < 1)
        {
            TempData["Error"] = "Sản phẩm đã hết hàng.";
            return RedirectToPage(new { slug });
        }

        cart.Add(new CartLine
        {
            ProductId = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            ImageUrl = product.ImageUrl,
            UnitPrice = product.DiscountPrice ?? product.Price,
            Quantity = quantity
        });
        TempData["CartAdded"] = "1";
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostReviewAsync(string slug)
    {
        if (!await LoadAsync(slug))
            return NotFound();

        Review.CustomerName = TextHelper.Trimmed(Review.CustomerName);
        Review.Comment = TextHelper.Trimmed(Review.Comment);
        if (Review.CustomerName.Length is < 2 or > 150 || Review.Comment.Length is < 2 or > 2000 || Review.Rating is < 1 or > 5)
        {
            ModelState.AddModelError(string.Empty, "Nhập tên, đánh giá từ 1 đến 5 sao và nội dung nhận xét.");
            return Page();
        }

        int? userId = null;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdValue, out var parsed))
            userId = parsed;

        db.ProductReviews.Add(new ProductReview
        {
            ProductId = Product!.Id,
            UserId = userId,
            CustomerName = Review.CustomerName,
            Rating = Review.Rating,
            Comment = Review.Comment,
            IsApproved = false
        });
        await db.SaveChangesAsync();
        TempData["Message"] = "Đã gửi nhận xét, chờ duyệt.";
        return RedirectToPage(new { slug });
    }

    private async Task<bool> LoadAsync(string slug)
    {
        Product = await db.Products
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.IsVisible && item.Slug == slug);
        if (Product is null)
            return false;

        Reviews = await db.ProductReviews.AsNoTracking()
            .Where(item => item.ProductId == Product.Id && item.IsApproved)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();

        var relatedCategoryIds = await db.ProductCategories.AsNoTracking()
            .Where(link => link.ProductId == Product.Id)
            .Select(link => link.CategoryId)
            .ToListAsync();
        if (relatedCategoryIds.Count == 0 && Product.CategoryId > 0)
            relatedCategoryIds = [Product.CategoryId];

        RelatedProducts = await db.Products.AsNoTracking()
            .Where(item => item.IsVisible && item.Id != Product.Id &&
                item.ProductCategories.Any(link => relatedCategoryIds.Contains(link.CategoryId)))
            .OrderByDescending(item => item.CreatedAt)
            .Take(6)
            .ToListAsync();

        var categorySlug = await db.Categories.AsNoTracking()
            .Where(item => item.Id == Product.CategoryId)
            .Select(item => item.Slug)
            .FirstOrDefaultAsync();
        RelatedCatalogUrl = string.IsNullOrWhiteSpace(categorySlug)
            ? "/san-pham"
            : "/san-pham?cat=" + Uri.EscapeDataString(categorySlug);

		ViewData["Title"] = Product.MetaTitle ?? Product.Name;
        ViewData["Description"] = Product.MetaDescription ?? Product.ShortDescription;
        ViewData["OgType"] = "product";
        ViewData["OgImage"] = SiteSocial.PickImage(Product.SeoImage, Product.ImageUrl);
        ViewData["ImmersiveDetail"] = true;
        ViewData["ShopAssets"] = "detail";
        return true;
    }

    private Task<Product?> Visible(string slug) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(item => item.IsVisible && item.Slug == slug);

    public class ReviewInput
    {
        public string CustomerName { get; set; } = string.Empty;

        public int Rating { get; set; } = 5;

        public string Comment { get; set; } = string.Empty;
    }
}
