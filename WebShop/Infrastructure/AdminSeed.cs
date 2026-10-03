using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class AdminSeed
{
    public const string DefaultUsername = "admin";

    public const string DefaultPassword = "Admin@123";

    public static async Task EnsureAdminAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(user => user.Username == DefaultUsername))
            return;

        var adminRole = await db.Roles.FirstOrDefaultAsync(role => role.Name == "Admin");
        if (adminRole is null)
            return;

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
}
