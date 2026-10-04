using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

public static class ProductReviewSchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "ProductReviews" ADD COLUMN "ImageUrl" TEXT NULL;""", ct);
        }
        catch
        {
            // Column already exists.
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "ProductReviews" ADD COLUMN "Source" TEXT NOT NULL DEFAULT 'Web';""", ct);
        }
        catch
        {
            // Column already exists.
        }
    }
}
