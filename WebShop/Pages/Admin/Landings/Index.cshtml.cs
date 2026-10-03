using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Landings;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<LandingRow> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var pages = await db.LandingPages.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Name)
            .ToListAsync();

        var sources = pages.Select(item => "LP:" + item.Slug).ToList();
        var counts = sources.Count == 0
            ? new Dictionary<string, int>()
            : await db.Orders.AsNoTracking()
                .Where(order => order.Source != null && sources.Contains(order.Source))
                .GroupBy(order => order.Source!)
                .Select(group => new { Source = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Source, item => item.Count);

        Items = pages.Select(page => new LandingRow(
            page,
            counts.GetValueOrDefault("LP:" + page.Slug),
            HasTracking(page))).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var page = await db.LandingPages.FindAsync(id);
        if (page is null)
            return NotFound();

        db.LandingPages.Remove(page);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa landing page.";
        return RedirectToPage();
    }

    private static bool HasTracking(LandingPage page) =>
        !string.IsNullOrWhiteSpace(page.MetaPixelId)
        || !string.IsNullOrWhiteSpace(page.TikTokPixelId)
        || !string.IsNullOrWhiteSpace(page.HeadScripts)
        || !string.IsNullOrWhiteSpace(page.BodyScripts);

    public record LandingRow(LandingPage Page, int OrderCount, bool HasTracking);
}
