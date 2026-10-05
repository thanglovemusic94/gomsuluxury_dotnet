using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class ProductSchema
{
    private const string HidePriceAllMarker = "Products.HidePriceAll.v1";

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

        await EnsureContactPricingOnceAsync(db, ct);
    }

    /// <summary>
    /// Một lần mỗi môi trường (local/VPS): bật HidePrice cho mọi SP.
    /// Sau đó Admin vẫn tắt từng SP được — marker ngăn ghi đè lại.
    /// </summary>
    public static async Task EnsureContactPricingOnceAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.SystemSettings.AsNoTracking().AnyAsync(item => item.Key == HidePriceAllMarker, ct))
            return;

        await db.Database.ExecuteSqlRawAsync("""UPDATE "Products" SET "HidePrice" = 1;""", ct);
        db.SystemSettings.Add(new SystemSetting
        {
            Key = HidePriceAllMarker,
            Value = "1",
            Description = "Đã bật ẩn giá / CTA liên hệ cho mọi sản phẩm (chạy một lần khi deploy)."
        });
        await db.SaveChangesAsync(ct);
    }
}
