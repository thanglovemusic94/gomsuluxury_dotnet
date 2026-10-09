using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shop;

public class AboutModel(AppDbContext db, HtmlBlockService blocks, MediaTrashFilter trash) : PageModel
{
    public string BodyHtml { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var page = await db.CustomPages.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IsPublished && item.Slug == AboutPageSeed.Slug);
        if (page is null)
            return NotFound();

        BodyHtml = await blocks.ExpandAsync(page.Content);
        ViewData["Title"] = page.MetaTitle ?? page.Title;
        ViewData["Description"] = page.MetaDescription
            ?? "Gốm Sứ Luxury – tự hào gốm Việt. Vẻ đẹp thuần Việt, đẳng cấp toàn cầu.";
        ViewData["OgType"] = "article";
        ViewData["ShopAssets"] = "detail";

        var og = await trash.LiveOrNullAsync(page.SeoImage);
        if (!string.IsNullOrWhiteSpace(og))
        {
            ViewData["OgImage"] = og;
            ViewData["LcpImage"] = MediaUrls.For(og, MediaSize.Large);
        }

        return Page();
    }
}
