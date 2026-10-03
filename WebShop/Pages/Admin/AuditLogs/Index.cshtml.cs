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
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Action))
            query = query.Where(item => item.Action == Action);
        if (!string.IsNullOrWhiteSpace(EntityType))
            query = query.Where(item => item.EntityType == EntityType);
        if (!string.IsNullOrWhiteSpace(Username))
            query = query.Where(item => item.Username.Contains(Username));

        ApplyPagingTotals(await query.CountAsync());
        Items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/AuditLogs", page, size,
            ("Action", Action),
            ("EntityType", EntityType),
            ("Username", Username));
}
