using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

/// <summary>
/// Bảng nối Product ↔ Category (nhiều danh mục). CategoryId trên Product vẫn là danh mục chính.
/// </summary>
public static class ProductCategorySchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ProductCategories" (
                "ProductId" INTEGER NOT NULL,
                "CategoryId" INTEGER NOT NULL,
                CONSTRAINT "PK_ProductCategories" PRIMARY KEY ("ProductId", "CategoryId"),
                CONSTRAINT "FK_ProductCategories_Products_ProductId"
                    FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ProductCategories_Categories_CategoryId"
                    FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_ProductCategories_CategoryId" ON "ProductCategories" ("CategoryId");""",
            ct);

        // Đồng bộ danh mục chính vào bảng nối (không xóa liên kết phụ do Admin gán).
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT OR IGNORE INTO "ProductCategories" ("ProductId", "CategoryId")
            SELECT "Id", "CategoryId" FROM "Products"
            WHERE "CategoryId" IS NOT NULL AND "CategoryId" > 0;
            """, ct);
    }
}
