using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

public static class LandingPageSchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LandingPages" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LandingPages" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "RedirectWhenOff" TEXT NOT NULL DEFAULT 'Home',
                "Headline" TEXT NOT NULL,
                "Subheadline" TEXT NULL,
                "HeroImageUrl" TEXT NULL,
                "CtaText" TEXT NOT NULL DEFAULT 'Đặt mua ngay',
                "CtaUrl" TEXT NULL,
                "BodyHtml" TEXT NULL,
                "ThankYouMessage" TEXT NOT NULL,
                "OfferEndsAt" TEXT NULL,
                "MetaPixelId" TEXT NULL,
                "TikTokPixelId" TEXT NULL,
                "HeadScripts" TEXT NULL,
                "BodyScripts" TEXT NULL,
                "MetaTitle" TEXT NULL,
                "MetaDescription" TEXT NULL,
                "SeoImage" TEXT NULL,
                "SeoFocusKeyword" TEXT NULL,
                "SeoScore" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LandingPages_Slug" ON "LandingPages" ("Slug");""", ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "LandingPageProducts" (
                "LandingPageId" INTEGER NOT NULL,
                "ProductId" INTEGER NOT NULL,
                "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
                "AdsPrice" TEXT NULL,
                CONSTRAINT "PK_LandingPageProducts" PRIMARY KEY ("LandingPageId", "ProductId"),
                CONSTRAINT "FK_LandingPageProducts_LandingPages_LandingPageId"
                    FOREIGN KEY ("LandingPageId") REFERENCES "LandingPages" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_LandingPageProducts_Products_ProductId"
                    FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_LandingPageProducts_ProductId" ON "LandingPageProducts" ("ProductId");""", ct);

        foreach (var sql in new[]
                 {
                     """ALTER TABLE "LandingPages" ADD COLUMN "OfferEndsAt" TEXT NULL;""",
                     """ALTER TABLE "LandingPages" ADD COLUMN "MetaPixelId" TEXT NULL;""",
                     """ALTER TABLE "LandingPages" ADD COLUMN "TikTokPixelId" TEXT NULL;""",
                     """ALTER TABLE "LandingPages" ADD COLUMN "HeadScripts" TEXT NULL;""",
                     """ALTER TABLE "LandingPages" ADD COLUMN "BodyScripts" TEXT NULL;""",
                     """ALTER TABLE "LandingPageProducts" ADD COLUMN "AdsPrice" TEXT NULL;""",
                     """ALTER TABLE "Orders" ADD COLUMN "Source" TEXT NULL;"""
                 })
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch
            {
                // Column already exists.
            }
        }
    }
}
