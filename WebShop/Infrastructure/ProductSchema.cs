using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

public static class ProductSchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Products" ADD COLUMN "HidePrice" INTEGER NOT NULL DEFAULT 0;""", ct);
        }
        catch
        {
            // Column already exists.
        }
    }
}
