using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Users;

public class IndexModel(AppDbContext db) : PagedAdminPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Active { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Role { get; set; }

    public IList<User> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = Filtered();
        ApplyPagingTotals(await query.CountAsync());
        Items = await query
            .Include(user => user.Roles)
            .OrderBy(user => user.Username)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int id, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? q = null, string? active = null, string? role = null)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return NotFound();

        db.Users.Remove(user);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa thành viên.";
        return RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            Q = q,
            Active = active,
            Role = role
        });
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/Users", page, size,
            ("Q", Q),
            ("Active", Active),
            ("Role", Role));

    private IQueryable<User> Filtered()
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            query = query.Where(item =>
                item.Username.Contains(term) ||
                (item.FullName != null && item.FullName.Contains(term)) ||
                (item.Email != null && item.Email.Contains(term)));
        }

        if (Active == "1")
            query = query.Where(item => item.IsActive);
        else if (Active == "0")
            query = query.Where(item => !item.IsActive);

        if (!string.IsNullOrWhiteSpace(Role))
        {
            var role = Role.Trim();
            query = query.Where(item => item.Roles.Any(r => r.Name == role));
        }

        return query;
    }
}
