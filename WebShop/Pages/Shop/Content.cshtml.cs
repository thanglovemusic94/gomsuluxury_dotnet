using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shop;

public class ContentPageModel(AppDbContext db) : PageModel
{
    public Models.CustomPage? PageContent { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        slug = PagePaths.NormalizeSlug(slug);
        if (string.IsNullOrEmpty(slug) || PagePaths.IsReserved(slug))
            return NotFound();

        PageContent = await db.CustomPages.AsNoTracking().FirstOrDefaultAsync(item =>
            item.IsPublished && item.Slug == slug);
        if (PageContent is null)
            return NotFound();

        ViewData["Title"] = PageContent.MetaTitle ?? PageContent.Title;
        ViewData["Description"] = PageContent.MetaDescription;
        return Page();
    }
}
