using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class AdminSeed
{
    public const string DefaultUsername = "admin";
    public const string DefaultPassword = "Admin@123";

    public const string ViewerUsername = "viewer";
    public const string ViewerPassword = "Viewer@123";

    public static async Task EnsureAdminAsync(AppDbContext db)
    {
        await EnsureRoleAsync(db, AppRoles.Admin, "Quản trị toàn hệ thống");
        await EnsureRoleAsync(db, AppRoles.Staff, "Nhân viên vận hành đơn hàng và nội dung");
        await EnsureRoleAsync(db, AppRoles.Customer, "Khách hàng");
        await EnsureRoleAsync(db, AppRoles.Viewer, "Chỉ xem Admin — không tạo/sửa/xóa");

        if (!await db.Users.AnyAsync(user => user.Username == DefaultUsername))
        {
            var adminRole = await db.Roles.FirstAsync(role => role.Name == AppRoles.Admin);
            db.Users.Add(new User
            {
                Username = DefaultUsername,
                Email = "admin@webshop.local",
                FullName = "Quản trị",
                PasswordHash = TextHelper.HashPassword(DefaultPassword),
                IsActive = true,
                UserRoles = [new UserRole { RoleId = adminRole.Id }]
            });
            await db.SaveChangesAsync();
        }

        // Seed Viewer demo account once; never overwrite password if user already exists.
        if (!await db.Users.AnyAsync(user => user.Username == ViewerUsername))
        {
            var viewerRole = await db.Roles.FirstAsync(role => role.Name == AppRoles.Viewer);
            db.Users.Add(new User
            {
                Username = ViewerUsername,
                Email = "viewer@webshop.local",
                FullName = "Chỉ xem",
                PasswordHash = TextHelper.HashPassword(ViewerPassword),
                IsActive = true,
                UserRoles = [new UserRole { RoleId = viewerRole.Id }]
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureRoleAsync(AppDbContext db, string name, string description)
    {
        if (await db.Roles.AnyAsync(role => role.Name == name))
            return;

        db.Roles.Add(new Role { Name = name, Description = description });
        await db.SaveChangesAsync();
    }
}
