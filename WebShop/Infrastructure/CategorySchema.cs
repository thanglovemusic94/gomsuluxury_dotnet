using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

public static class CategorySchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Categories" ADD COLUMN "IsVisible" INTEGER NOT NULL DEFAULT 1;""", ct);
        }
        catch
        {
            // Column already exists.
        }
    }
}
