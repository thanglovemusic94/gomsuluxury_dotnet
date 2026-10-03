using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Products;

public class EditModel(AppDbContext db, AuditService audit, GeminiSeoService geminiSeo) : PageModel
{
    [BindProperty]
    public ProductInput Input { get; set; } = new();

    [BindProperty]
    public SeoInput Seo { get; set; } = new();

    public IList<Category> Categories { get; private set; } = [];

    public IList<ProductImage> Images { get; private set; } = [];

    public IList<ProductReview> Reviews { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadCategoriesAsync();
        if (id is null)
        {
            Input.IsVisible = true;
            return Page();
        }

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (product is null)
            return NotFound();

        Input = ProductInput.From(product);
        Input.CategoryIds = await db.ProductCategories.AsNoTracking()
            .Where(link => link.ProductId == product.Id)
            .Select(link => link.CategoryId)
            .ToListAsync();
        if (Input.CategoryIds.Count == 0 && product.CategoryId > 0)
            Input.CategoryIds = [product.CategoryId];
        Seo = SeoInput.From(product);
        await LoadChildrenAsync(product.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCategoriesAsync();
        Validate();
        if (!ModelState.IsValid)
        {
            if (Input.Id != 0)
                await LoadChildrenAsync(Input.Id);
            return Page();
        }

        var slug = TextHelper.Slug(Input.Slug, Input.Name);
        Product product;
        var isCreate = Input.Id == 0;
        Dictionary<string, string?>? before = null;
        if (isCreate)
        {
            product = new Product();
            db.Products.Add(product);
        }
        else
        {
            var existing = await db.Products.FirstOrDefaultAsync(item => item.Id == Input.Id);
            if (existing is null)
                return NotFound();
            product = existing;
            before = SnapshotProduct(product);
        }

        if (await db.Products.AnyAsync(item => item.Slug == slug && item.Id != product.Id))
        {
            ModelState.AddModelError("Input.Slug", "Slug đã tồn tại.");
            if (Input.Id != 0)
                await LoadChildrenAsync(Input.Id);
            return Page();
        }

        Input.Apply(product, slug);
        Seo.Apply(product);
        if (!await DbSave.TrySaveAsync(db, ModelState))
        {
            if (Input.Id != 0)
                await LoadChildrenAsync(Input.Id);
            return Page();
        }

        await SyncProductCategoriesAsync(product.Id, Input.CategoryId, Input.CategoryIds);

        if (isCreate)
        {
            await audit.LogAsync(AuditActions.Create, AuditEntities.Product, product.Id, product.Name, "Tạo sản phẩm mới");
        }
        else if (before is not null)
        {
            var changes = AuditService.Diff(before, SnapshotProduct(product));
            if (changes.Count > 0)
                await audit.LogAsync(AuditActions.Update, AuditEntities.Product, product.Id, product.Name,
                    $"Sửa {changes.Count} trường", string.Join("\n", changes));
        }

        TempData["Message"] = "Đã lưu sản phẩm.";
        return RedirectToPage("Edit", new { id = product.Id });
    }

    public async Task<IActionResult> OnPostAnalyzeSeoAsync([FromForm] SeoAiForm form, CancellationToken ct)
    {
        var result = await geminiSeo.AnalyzeProductAsync(new SeoAiRequest
        {
            EntityType = "Product",
            Name = form.Name ?? string.Empty,
            Slug = form.Slug ?? string.Empty,
            FocusKeyword = form.FocusKeyword,
            MetaTitle = form.MetaTitle,
            MetaDescription = form.MetaDescription,
            ImageUrl = form.ImageUrl,
            SeoImage = form.SeoImage,
            ShortDescription = form.ShortDescription,
            Description = form.Description
        }, ct);

        return new JsonResult(result);
    }

    public sealed class SeoAiForm
    {
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? FocusKeyword { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? ImageUrl { get; set; }
        public string? SeoImage { get; set; }
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
    }

    private static Dictionary<string, string?> SnapshotProduct(Product product) => new()
    {
        ["Name"] = product.Name,
        ["Slug"] = product.Slug,
        ["Price"] = product.Price.ToString("0.##"),
        ["DiscountPrice"] = product.DiscountPrice?.ToString("0.##"),
        ["CostPrice"] = product.CostPrice.ToString("0.##"),
        ["Stock"] = product.Stock.ToString(),
        ["ImageUrl"] = product.ImageUrl,
        ["IsVisible"] = product.IsVisible.ToString(),
        ["ShortDescription"] = product.ShortDescription,
        ["Description"] = product.Description,
        ["MetaTitle"] = product.MetaTitle,
        ["CategoryId"] = product.CategoryId.ToString()
    };

    public async Task<IActionResult> OnPostAddImageAsync(int id, string imageUrl, string? altText, int displayOrder)
    {
        var product = await db.Products.AnyAsync(item => item.Id == id);
        if (!product)
            return NotFound();
        if (string.IsNullOrWhiteSpace(imageUrl) || imageUrl.Trim().Length > 500 || altText?.Length > 200)
        {
            TempData["Error"] = "URL ảnh tối đa 500 ký tự, alt text tối đa 200 ký tự.";
            return RedirectToPage(new { id });
        }

        db.ProductImages.Add(new ProductImage
        {
            ProductId = id,
            ImageUrl = imageUrl.Trim(),
            AltText = TextHelper.Clean(altText),
            DisplayOrder = displayOrder
        });
        await db.SaveChangesAsync();
        TempData["Message"] = "Đã thêm ảnh.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteImageAsync(int id, int imageId)
    {
        var image = await db.ProductImages.FirstOrDefaultAsync(item => item.Id == imageId && item.ProductId == id);
        if (image is null)
            return NotFound();

        db.ProductImages.Remove(image);
        await db.SaveChangesAsync();
        TempData["Message"] = "Đã xóa ảnh.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveReviewAsync(int id, int reviewId)
    {
        var review = await db.ProductReviews.FirstOrDefaultAsync(item => item.Id == reviewId && item.ProductId == id);
        if (review is null)
            return NotFound();

        review.IsApproved = !review.IsApproved;
        await db.SaveChangesAsync();
        TempData["Message"] = review.IsApproved ? "Đã duyệt đánh giá." : "Đã ẩn đánh giá.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteReviewAsync(int id, int reviewId)
    {
        var review = await db.ProductReviews.FirstOrDefaultAsync(item => item.Id == reviewId && item.ProductId == id);
        if (review is null)
            return NotFound();

        db.ProductReviews.Remove(review);
        await db.SaveChangesAsync();
        TempData["Message"] = "Đã xóa đánh giá.";
        return RedirectToPage(new { id });
    }

    private void Validate()
    {
        Input.Name = TextHelper.Trimmed(Input.Name);
        Input.ImageUrl = TextHelper.Trimmed(Input.ImageUrl);
        Input.Description = TextHelper.Trimmed(Input.Description);
        if (Input.Name.Length is < 1 or > 200)
            ModelState.AddModelError("Input.Name", "Tên từ 1 đến 200 ký tự.");
        if (Input.ImageUrl.Length is < 1 or > 500)
            ModelState.AddModelError("Input.ImageUrl", "Nhập URL ảnh đại diện, tối đa 500 ký tự.");
        if (string.IsNullOrWhiteSpace(Input.Description))
            ModelState.AddModelError("Input.Description", "Nhập mô tả.");
        if (Input.ShortDescription?.Length > 500)
            ModelState.AddModelError("Input.ShortDescription", "Mô tả ngắn tối đa 500 ký tự.");
        if (Input.Price < 0 || Input.CostPrice < 0 || Input.DiscountPrice < 0)
            ModelState.AddModelError(string.Empty, "Giá không được âm.");
        if (Input.Stock < 0)
            ModelState.AddModelError("Input.Stock", "Tồn kho không được âm.");
        if (!Categories.Any(category => category.Id == Input.CategoryId))
            ModelState.AddModelError("Input.CategoryId", "Chọn danh mục chính loại Product.");
        Input.CategoryIds ??= [];
        Input.CategoryIds = Input.CategoryIds
            .Where(id => id > 0 && Categories.Any(category => category.Id == id))
            .Distinct()
            .ToList();
        if (!Input.CategoryIds.Contains(Input.CategoryId) && Input.CategoryId > 0)
            Input.CategoryIds.Add(Input.CategoryId);
        Seo.Validate(ModelState);
    }

    private async Task SyncProductCategoriesAsync(int productId, int primaryCategoryId, IList<int>? categoryIds)
    {
        var wanted = (categoryIds ?? [])
            .Where(id => id > 0)
            .Append(primaryCategoryId)
            .Where(id => id > 0)
            .Distinct()
            .ToHashSet();

        var existing = await db.ProductCategories
            .Where(link => link.ProductId == productId)
            .ToListAsync();

        foreach (var link in existing.Where(link => !wanted.Contains(link.CategoryId)))
            db.ProductCategories.Remove(link);

        var have = existing.Select(link => link.CategoryId).ToHashSet();
        foreach (var categoryId in wanted.Where(id => !have.Contains(id)))
        {
            db.ProductCategories.Add(new ProductCategory
            {
                ProductId = productId,
                CategoryId = categoryId
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        Categories = await db.Categories.AsNoTracking()
            .Where(category => category.Type == "Product")
            .OrderBy(category => category.Name)
            .ToListAsync();
    }

    private async Task LoadChildrenAsync(int productId)
    {
        Images = await db.ProductImages.AsNoTracking()
            .Where(image => image.ProductId == productId)
            .OrderBy(image => image.DisplayOrder)
            .ToListAsync();
        Reviews = await db.ProductReviews.AsNoTracking()
            .Where(review => review.ProductId == productId)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync();
    }

    public class ProductInput
    {
        public int Id { get; set; }

        public int CategoryId { get; set; }

        /// <summary>Tất cả danh mục (gồm chính). Bind từ checkbox.</summary>
        public List<int> CategoryIds { get; set; } = [];

        public string Name { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public decimal Price { get; set; }

        public decimal? DiscountPrice { get; set; }

        public decimal CostPrice { get; set; }

        public int Stock { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;

        public static ProductInput From(Product product) => new()
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            Name = product.Name,
            Slug = product.Slug,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            CostPrice = product.CostPrice,
            Stock = product.Stock,
            Description = product.Description,
            ShortDescription = product.ShortDescription,
            ImageUrl = product.ImageUrl,
            IsVisible = product.IsVisible
        };

        public void Apply(Product product, string slug)
        {
            product.CategoryId = CategoryId;
            product.Name = Name;
            product.Slug = slug;
            product.Price = Price;
            product.DiscountPrice = DiscountPrice;
            product.CostPrice = CostPrice;
            product.Stock = Stock;
            product.Description = Description;
            product.ShortDescription = TextHelper.Clean(ShortDescription);
            product.ImageUrl = ImageUrl;
            product.IsVisible = IsVisible;
        }
    }
}
