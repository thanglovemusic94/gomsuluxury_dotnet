using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.HtmlBlocks;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<HtmlBlock> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await db.HtmlBlocks.AsNoTracking()
            .OrderBy(block => block.Key)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, HtmlBlockService blocks)
    {
        var item = await db.HtmlBlocks.FindAsync(id);
        if (item is null)
            return NotFound();

        var key = item.Key;
        db.HtmlBlocks.Remove(item);
        var error = await DbSave.TryDeleteAsync(db);
        if (error is null)
            blocks.Invalidate(key);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa block.";
        return RedirectToPage();
    }
}
