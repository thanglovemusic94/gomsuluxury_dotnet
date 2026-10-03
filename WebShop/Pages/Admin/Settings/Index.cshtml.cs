using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Settings;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<SystemSetting> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await db.SystemSettings.AsNoTracking().OrderBy(setting => setting.Key).ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var setting = await db.SystemSettings.FindAsync(id);
        if (setting is null)
            return NotFound();

        db.SystemSettings.Remove(setting);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa cấu hình.";
        return RedirectToPage();
    }
}
