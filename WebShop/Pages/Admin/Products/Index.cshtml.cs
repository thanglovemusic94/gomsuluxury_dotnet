using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Products;

public class IndexModel(AppDbContext db, AuditService audit) : PagedAdminPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Visible { get; set; }

    public IList<Product> Items { get; private set; } = [];

    public List<SelectListItem> CategoryOptions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        CategoryOptions = await db.Categories.AsNoTracking()
            .Where(item => item.Type == "Product")
            .OrderBy(item => item.Name)
            .Select(item => new SelectListItem(item.Name, item.Id.ToString(), CategoryId == item.Id))
            .ToListAsync();

        var query = Filtered();
        ApplyPagingTotals(await query.CountAsync());
        Items = await query
            .Include(product => product.Category)
            .OrderByDescending(product => product.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int id, string? mode = null, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? q = null, int? categoryId = null, string? visible = null)
    {
        if (AdminContentDelete.IsHard(mode) && !AppRoles.IsAdmin(User))
            return Forbid();

        var (count, error) = await AdminContentDelete.DeleteProductsAsync(db, audit, [id], mode, User.Identity?.Name);
        if (error is not null)
            TempData["Error"] = error;
        else if (count > 0)
            TempData["Message"] = AdminContentDelete.IsHard(mode)
                ? "Đã xóa vĩnh viễn sản phẩm."
                : "Đã chuyển sản phẩm vào thùng rác.";

        return RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            Q = q,
            CategoryId = categoryId,
            Visible = visible
        });
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync(
        List<int>? ids, string? mode = null, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? q = null, int? categoryId = null, string? visible = null)
    {
        if (AdminContentDelete.IsHard(mode) && !AppRoles.IsAdmin(User))
            return Forbid();

        var (count, error) = await AdminContentDelete.DeleteProductsAsync(db, audit, ids ?? [], mode, User.Identity?.Name);
        if (error is not null)
            TempData["Error"] = error;
        else if (count == 0)
            TempData["Error"] = "Chưa chọn sản phẩm nào.";
        else
            TempData["Message"] = AdminContentDelete.IsHard(mode)
                ? $"Đã xóa vĩnh viễn {count} sản phẩm."
                : $"Đã chuyển {count} sản phẩm vào thùng rác.";

        return RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            Q = q,
            CategoryId = categoryId,
            Visible = visible
        });
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/Products", page, size,
            ("Q", Q),
            ("CategoryId", CategoryId?.ToString()),
            ("Visible", Visible));

    private IQueryable<Product> Filtered()
    {
        var query = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            query = query.Where(item => item.Name.Contains(term) || item.Slug.Contains(term));
        }

        if (CategoryId is > 0)
            query = query.Where(item => item.ProductCategories.Any(link => link.CategoryId == CategoryId));

        if (Visible == "1")
            query = query.Where(item => item.IsVisible);
        else if (Visible == "0")
            query = query.Where(item => !item.IsVisible);

        return query;
    }
}
