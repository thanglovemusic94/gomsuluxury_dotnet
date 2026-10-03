using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class ShopNavSeed
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        var parent = await db.Menus.FirstOrDefaultAsync(menu =>
            menu.Position == "Header" && menu.ParentId == null && menu.Name == "Về chúng tôi");
        if (parent is null)
        {
            parent = new Menu
            {
                Name = "Về chúng tôi",
                Url = "/gioi-thieu",
                Position = "Header",
                DisplayOrder = 6,
                IsActive = true
            };
            db.Menus.Add(parent);
            await db.SaveChangesAsync();
        }

        var links = new (string Name, string Url, int Order)[]
        {
            ("Giới thiệu", "/gioi-thieu", 1),
            ("Chính sách giao hàng", "/chinh-sach-giao-hang", 2),
            ("Chính sách đổi trả", "/chinh-sach-doi-tra", 3)
        };
        foreach (var link in links)
        {
            if (await db.Menus.AnyAsync(menu => menu.ParentId == parent.Id && menu.Url == link.Url))
                continue;

            db.Menus.Add(new Menu
            {
                Name = link.Name,
                Url = link.Url,
                ParentId = parent.Id,
                Position = "Header",
                DisplayOrder = link.Order,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }
}
