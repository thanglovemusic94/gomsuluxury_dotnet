using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

/// <summary>
/// Trừ/hoàn tồn kho atomic (SQL UPDATE có điều kiện) — tránh oversell khi nhiều request cùng chốt 1 SKU.
/// </summary>
public static class StockInventory
{
    public static async Task<bool> TryDecrementAsync(
        AppDbContext db,
        int productId,
        int quantity,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            return false;

        var affected = await db.Products
            .Where(product => product.Id == productId && product.Stock >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(product => product.Stock, product => product.Stock - quantity),
                ct);

        return affected == 1;
    }

    public static async Task RestoreAsync(
        AppDbContext db,
        int productId,
        int quantity,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            return;

        await db.Products
            .Where(product => product.Id == productId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(product => product.Stock, product => product.Stock + quantity),
                ct);
    }

    public static async Task<(bool Ok, string? Error)> TryDecrementManyAsync(
        AppDbContext db,
        IReadOnlyList<(int ProductId, int Quantity, string Name)> lines,
        CancellationToken ct = default)
    {
        var applied = new List<(int ProductId, int Quantity)>();
        foreach (var line in lines)
        {
            if (await TryDecrementAsync(db, line.ProductId, line.Quantity, ct))
            {
                applied.Add((line.ProductId, line.Quantity));
                continue;
            }

            foreach (var prev in applied)
                await RestoreAsync(db, prev.ProductId, prev.Quantity, ct);

            return (false, $"Không đủ tồn kho: {line.Name}.");
        }

        return (true, null);
    }
}
