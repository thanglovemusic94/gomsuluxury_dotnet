using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Pages.Shop;

public class CategoryModel(AppDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var exists = await db.Categories.AsNoTracking()
            .AnyAsync(item => item.Type == "Product" && item.Slug == slug);
        if (!exists)
            return NotFound();

        return RedirectPermanent($"/san-pham?cat={Uri.EscapeDataString(slug)}");
    }
}
