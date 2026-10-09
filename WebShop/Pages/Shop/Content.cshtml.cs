using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shop;

public class ContentPageModel(AppDbContext db, HtmlBlockService blocks, MediaTrashFilter trash) : PageModel
{
    public Models.CustomPage? PageContent { get; private set; }

    public string BodyHtml { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        slug = PagePaths.NormalizeSlug(slug);
        if (string.IsNullOrEmpty(slug) || PagePaths.IsReserved(slug))
            return NotFound();

        PageContent = await db.CustomPages.AsNoTracking().FirstOrDefaultAsync(item =>
            item.IsPublished && item.Slug == slug);
        if (PageContent is null)
            return NotFound();

        // Expand {{ block:key }} shortcodes (same as blog / product description).
        BodyHtml = await blocks.ExpandAsync(PageContent.Content);
        ViewData["Title"] = PageContent.MetaTitle ?? PageContent.Title;
        ViewData["Description"] = PageContent.MetaDescription;
        ViewData["OgType"] = "article";
        ViewData["OgImage"] = await trash.LiveOrNullAsync(PageContent.SeoImage);
        return Page();
    }
}
