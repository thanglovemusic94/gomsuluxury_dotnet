using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

public static class SoftDeleteSchema
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "AuditLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NULL,
                "Username" TEXT NOT NULL,
                "Action" TEXT NOT NULL,
                "EntityType" TEXT NOT NULL,
                "EntityId" INTEGER NOT NULL,
                "EntityTitle" TEXT NULL,
                "Summary" TEXT NULL,
                "Details" TEXT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_AuditLogs_CreatedAt" ON "AuditLogs" ("CreatedAt");""", ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Entity" ON "AuditLogs" ("EntityType", "EntityId");""", ct);

        await EnsureSoftColumnsAsync(db, "Products", ct);
        await EnsureSoftColumnsAsync(db, "Posts", ct);
        await EnsureSoftColumnsAsync(db, "CustomPages", ct);
        await EnsureSoftColumnsAsync(db, "MediaAssets", ct);
    }

    private static async Task EnsureSoftColumnsAsync(AppDbContext db, string table, CancellationToken ct)
    {
        await TryAlterAsync(db, $"""ALTER TABLE "{table}" ADD COLUMN "IsDeleted" INTEGER NOT NULL DEFAULT 0;""", ct);
        await TryAlterAsync(db, $"""ALTER TABLE "{table}" ADD COLUMN "DeletedAt" TEXT NULL;""", ct);
        await TryAlterAsync(db, $"""ALTER TABLE "{table}" ADD COLUMN "DeletedBy" TEXT NULL;""", ct);
    }

    private static async Task TryAlterAsync(AppDbContext db, string sql, CancellationToken ct)
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
