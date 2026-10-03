using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class BlogModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Post> Posts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Posts = await db.Posts.AsNoTracking()
            .Include(item => item.Category)
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
    }
}
