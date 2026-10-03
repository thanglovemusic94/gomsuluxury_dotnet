using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Posts;

public class IndexModel(AppDbContext db, AuditService audit) : PagedAdminPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Published { get; set; }

    public IList<Post> Items { get; private set; } = [];

    public List<SelectListItem> CategoryOptions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        CategoryOptions = await db.Categories.AsNoTracking()
            .Where(item => item.Type == "Blog")
            .OrderBy(item => item.Name)
            .Select(item => new SelectListItem(item.Name, item.Id.ToString(), CategoryId == item.Id))
            .ToListAsync();

        var query = Filtered();
        ApplyPagingTotals(await query.CountAsync());
        Items = await query
            .Include(post => post.Category)
            .Include(post => post.Author)
            .OrderByDescending(post => post.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int id, string? mode = null, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? q = null, int? categoryId = null, string? published = null)
    {
        if (AdminContentDelete.IsHard(mode) && !AppRoles.IsAdmin(User))
            return Forbid();

        var (count, error) = await AdminContentDelete.DeletePostsAsync(db, audit, [id], mode, User.Identity?.Name);
        if (error is not null)
            TempData["Error"] = error;
        else if (count > 0)
            TempData["Message"] = AdminContentDelete.IsHard(mode)
                ? "Đã xóa vĩnh viễn bài viết."
                : "Đã chuyển bài viết vào thùng rác.";

        return RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            Q = q,
            CategoryId = categoryId,
            Published = published
        });
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync(
        List<int>? ids, string? mode = null, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? q = null, int? categoryId = null, string? published = null)
    {
        if (AdminContentDelete.IsHard(mode) && !AppRoles.IsAdmin(User))
            return Forbid();

        var (count, error) = await AdminContentDelete.DeletePostsAsync(db, audit, ids ?? [], mode, User.Identity?.Name);
        if (error is not null)
            TempData["Error"] = error;
        else if (count == 0)
            TempData["Error"] = "Chưa chọn bài viết nào.";
        else
            TempData["Message"] = AdminContentDelete.IsHard(mode)
                ? $"Đã xóa vĩnh viễn {count} bài viết."
                : $"Đã chuyển {count} bài viết vào thùng rác.";

        return RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            Q = q,
            CategoryId = categoryId,
            Published = published
        });
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/Posts", page, size,
            ("Q", Q),
            ("CategoryId", CategoryId?.ToString()),
            ("Published", Published));

    private IQueryable<Post> Filtered()
    {
        var query = db.Posts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            query = query.Where(item => item.Title.Contains(term) || item.Slug.Contains(term));
        }

        if (CategoryId is > 0)
            query = query.Where(item => item.CategoryId == CategoryId);

        if (Published == "1")
            query = query.Where(item => item.IsPublished);
        else if (Published == "0")
            query = query.Where(item => !item.IsPublished);

        return query;
    }
}
