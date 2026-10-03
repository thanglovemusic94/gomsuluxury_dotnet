using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Pages.Admin.HtmlBlocks;

public class DetailModel(AppDbContext db) : PageModel
{
    public HtmlBlock Item { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var item = await db.HtmlBlocks.AsNoTracking().FirstOrDefaultAsync(block => block.Id == id);
        if (item is null)
            return NotFound();

        Item = item;
        return Page();
    }
}
