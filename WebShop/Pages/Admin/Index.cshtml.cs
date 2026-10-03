using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Pages.Admin;

public class IndexModel(AppDbContext db) : PageModel
{
    public int UserCount { get; private set; }

    public int ProductCount { get; private set; }

    public int OrderCount { get; private set; }

    public int PendingOrderCount { get; private set; }

    public int PostCount { get; private set; }

    public int CategoryCount { get; private set; }

    public int SettingCount { get; private set; }

    public async Task OnGetAsync()
    {
        UserCount = await db.Users.CountAsync();
        ProductCount = await db.Products.CountAsync();
        OrderCount = await db.Orders.CountAsync();
        PendingOrderCount = await db.Orders.CountAsync(order => order.Status == "Pending");
        PostCount = await db.Posts.CountAsync();
        CategoryCount = await db.Categories.CountAsync();
        SettingCount = await db.SystemSettings.CountAsync();
    }
}
