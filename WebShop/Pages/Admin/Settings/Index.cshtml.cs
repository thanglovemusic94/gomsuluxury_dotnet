using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Settings;

public class IndexModel(AppDbContext db, SiteSeoService siteSeo) : PageModel
{
    public IList<SystemSetting> Items { get; private set; } = [];

    [BindProperty]
    public bool AllowIndexing { get; set; }

    public async Task OnGetAsync()
    {
        AllowIndexing = await siteSeo.GetAllowIndexingAsync();
        Items = await db.SystemSettings.AsNoTracking()
            .Where(setting => setting.Key != SiteSeoService.AllowIndexingKey)
            .OrderBy(setting => setting.Key)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostSeoAsync()
    {
        await siteSeo.SetAllowIndexingAsync(AllowIndexing);
        TempData["Message"] = AllowIndexing
            ? "Đã bật index cho công cụ tìm kiếm (Google)."
            : "Đã tắt index — Google sẽ không được khuyến khích quét site.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var setting = await db.SystemSettings.FindAsync(id);
        if (setting is null)
            return NotFound();

        if (setting.Key == SiteSeoService.AllowIndexingKey)
        {
            TempData["Error"] = "Không xóa khóa SEO index tại đây — dùng nút bên trên.";
            return RedirectToPage();
        }

        db.SystemSettings.Remove(setting);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa cấu hình.";
        return RedirectToPage();
    }
}
