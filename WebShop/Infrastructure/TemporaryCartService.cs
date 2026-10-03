using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public sealed class TemporaryCartService(AppDbContext db)
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "TemporaryCarts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TemporaryCarts" PRIMARY KEY AUTOINCREMENT,
                "Token" TEXT NOT NULL,
                "ProductId" INTEGER NOT NULL,
                "Sku" TEXT NOT NULL,
                "Quantity" INTEGER NOT NULL DEFAULT 1,
                "UnitPrice" TEXT NOT NULL,
                "CustomerPhone" TEXT NULL,
                "FbUserId" TEXT NULL,
                "Status" TEXT NOT NULL DEFAULT 'Pending',
                "IsDemo" INTEGER NOT NULL DEFAULT 0,
                "ExpiresAt" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "OrderId" INTEGER NULL,
                CONSTRAINT "FK_TemporaryCarts_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TemporaryCarts_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE SET NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TemporaryCarts_Token" ON "TemporaryCarts" ("Token");""", ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_TemporaryCarts_Status" ON "TemporaryCarts" ("Status");""", ct);
    }

    public async Task<(TemporaryCart? Cart, string? Error)> CreateAsync(
        int productId,
        string? phone,
        string? fbUserId = null,
        string? sku = null,
        int quantity = 1,
        bool isDemo = false,
        CancellationToken ct = default)
    {
        quantity = Math.Clamp(quantity, 1, 99);
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == productId && item.IsVisible, ct);
        if (product is null)
            return (null, "Không tìm thấy sản phẩm.");
        if (product.Stock < quantity)
            return (null, $"Không đủ tồn kho (còn {product.Stock}).");

        var cart = new TemporaryCart
        {
            Token = NewToken(),
            ProductId = product.Id,
            Sku = string.IsNullOrWhiteSpace(sku) ? product.Slug : sku.Trim().ToUpperInvariant(),
            Quantity = quantity,
            UnitPrice = product.DiscountPrice ?? product.Price,
            CustomerPhone = TextHelper.Clean(phone),
            FbUserId = TextHelper.Clean(fbUserId),
            Status = TemporaryCartStatuses.Pending,
            IsDemo = isDemo,
            ExpiresAt = DateTime.UtcNow.Add(DefaultTtl),
            CreatedAt = DateTime.UtcNow
        };

        db.TemporaryCarts.Add(cart);
        await db.SaveChangesAsync(ct);
        return (cart, null);
    }

    public async Task<TemporaryCart?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        token = (token ?? string.Empty).Trim();
        if (token.Length is < 8 or > 64)
            return null;

        var cart = await db.TemporaryCarts
            .Include(item => item.Product)
            .FirstOrDefaultAsync(item => item.Token == token, ct);
        if (cart is null)
            return null;

        if (cart.Status == TemporaryCartStatuses.Pending && cart.ExpiresAt < DateTime.UtcNow)
        {
            cart.Status = TemporaryCartStatuses.Expired;
            await db.SaveChangesAsync(ct);
        }

        return cart;
    }

    public string CheckoutPath(string token) => $"/chot/{Uri.EscapeDataString(token)}";

    private static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[18];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
