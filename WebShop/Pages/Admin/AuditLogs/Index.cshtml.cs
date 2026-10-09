using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.AuditLogs;

public class IndexModel(AppDbContext db) : PagedAdminPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Action { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EntityType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Username { get; set; }

    public IList<AuditLog> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = Filter(db.AuditLogs.AsNoTracking());
        ApplyPagingTotals(await query.CountAsync());
        Items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync(
        long[]? ids,
        int pageNumber = 1,
        int pageSize = AdminPaging.DefaultPageSize,
        string? action = null,
        string? entityType = null,
        string? username = null)
    {
        var idList = (ids ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (idList.Count == 0)
        {
            TempData["Error"] = "Chọn ít nhất một bản ghi nhật ký.";
            return RedirectList(pageNumber, pageSize, action, entityType, username);
        }

        var removed = await db.AuditLogs
            .Where(item => idList.Contains(item.Id))
            .ExecuteDeleteAsync();

        TempData["Message"] = removed == 0
            ? "Không xóa được bản ghi nào."
            : $"Đã xóa {removed} bản ghi nhật ký.";

        return RedirectList(pageNumber, pageSize, action, entityType, username);
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/AuditLogs", page, size,
            ("Action", Action),
            ("EntityType", EntityType),
            ("Username", Username));

    private IQueryable<AuditLog> Filter(IQueryable<AuditLog> query)
    {
        if (!string.IsNullOrWhiteSpace(Action))
            query = query.Where(item => item.Action == Action);
        if (!string.IsNullOrWhiteSpace(EntityType))
            query = query.Where(item => item.EntityType == EntityType);
        if (!string.IsNullOrWhiteSpace(Username))
            query = query.Where(item => item.Username.Contains(Username));
        return query;
    }

    private IActionResult RedirectList(int pageNumber, int pageSize, string? action, string? entityType, string? username) =>
        RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            Action = action,
            EntityType = entityType,
            Username = username
        });
}
