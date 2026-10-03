using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.StaticPages;

public class IndexModel(AppDbContext db, AuditService audit) : PageModel
{
    public IList<CustomPage> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await db.CustomPages.AsNoTracking().OrderBy(page => page.Title).ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? mode = null)
    {
        if (AdminContentDelete.IsHard(mode) && !AppRoles.IsAdmin(User))
            return Forbid();

        var (count, error) = await AdminContentDelete.DeletePagesAsync(db, audit, [id], mode, User.Identity?.Name);
        if (error is not null)
            TempData["Error"] = error;
        else if (count > 0)
            TempData["Message"] = AdminContentDelete.IsHard(mode)
                ? "Đã xóa vĩnh viễn trang."
                : "Đã chuyển trang vào thùng rác.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync(List<int>? ids, string? mode = null)
    {
        if (AdminContentDelete.IsHard(mode) && !AppRoles.IsAdmin(User))
            return Forbid();

        var (count, error) = await AdminContentDelete.DeletePagesAsync(db, audit, ids ?? [], mode, User.Identity?.Name);
        if (error is not null)
            TempData["Error"] = error;
        else if (count == 0)
            TempData["Error"] = "Chưa chọn trang nào.";
        else
            TempData["Message"] = AdminContentDelete.IsHard(mode)
                ? $"Đã xóa vĩnh viễn {count} trang."
                : $"Đã chuyển {count} trang vào thùng rác.";

        return RedirectToPage();
    }
}
