using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

/// <summary>
/// OrderItems / Reviews use Restrict — hard-delete from product list stays blocked;
/// Trash can force-clear related rows after an explicit warning.
/// </summary>
public static class ProductDeleteGuard
{
    public static async Task<ProductDeleteBlockers> GetBlockersAsync(
        AppDbContext db, int productId, CancellationToken ct = default)
    {
        var orderLines = await db.OrderItems.AsNoTracking()
            .CountAsync(item => item.ProductId == productId, ct);
        var orders = orderLines == 0
            ? 0
            : await db.OrderItems.AsNoTracking()
                .Where(item => item.ProductId == productId)
                .Select(item => item.OrderId)
                .Distinct()
                .CountAsync(ct);
        var reviews = await db.ProductReviews.AsNoTracking()
            .CountAsync(item => item.ProductId == productId, ct);
        return new ProductDeleteBlockers(orders, orderLines, reviews);
    }

    /// <summary>Block message for hard-delete from product list (no force).</summary>
    public static string? Message(ProductDeleteBlockers b)
    {
        if (!b.IsBlocked)
            return null;

        return "Không xóa cứng được vì sản phẩm còn trong "
               + RelatedParts(b)
               + ". Đưa vào thùng rác rồi xóa cứng tại đó nếu vẫn muốn (sẽ gỡ dòng đơn / đánh giá liên quan).";
    }

    /// <summary>Warning shown in Trash before force hard-delete.</summary>
    public static string? TrashWarning(ProductDeleteBlockers b)
    {
        if (!b.IsBlocked)
            return null;

        return "Sản phẩm còn trong " + RelatedParts(b)
               + ". Xóa cứng sẽ gỡ các dòng đơn và đánh giá liên quan — không hoàn tác.";
    }

    /// <summary>Removes Restrict FK rows so the product can be hard-deleted from Trash.</summary>
    public static async Task ClearRelatedAsync(
        AppDbContext db, int productId, CancellationToken ct = default)
    {
        await db.OrderItems.Where(item => item.ProductId == productId).ExecuteDeleteAsync(ct);
        await db.ProductReviews.Where(item => item.ProductId == productId).ExecuteDeleteAsync(ct);
    }

    private static string RelatedParts(ProductDeleteBlockers b)
    {
        var parts = new List<string>();
        if (b.Orders > 0)
            parts.Add($"{b.Orders} đơn hàng ({b.OrderLines} dòng chi tiết)");
        else if (b.OrderLines > 0)
            parts.Add($"{b.OrderLines} dòng đơn hàng");

        if (b.Reviews > 0)
            parts.Add($"{b.Reviews} đánh giá");

        return string.Join(" và ", parts);
    }
}

public readonly record struct ProductDeleteBlockers(int Orders, int OrderLines, int Reviews)
{
    public bool IsBlocked => Orders > 0 || OrderLines > 0 || Reviews > 0;

    public string Summary
    {
        get
        {
            if (!IsBlocked)
                return "Có thể xóa cứng (không còn đơn / đánh giá).";
            var parts = new List<string>();
            if (Orders > 0)
                parts.Add($"{Orders} đơn ({OrderLines} dòng)");
            else if (OrderLines > 0)
                parts.Add($"{OrderLines} dòng đơn");
            if (Reviews > 0)
                parts.Add($"{Reviews} đánh giá");
            return "Cảnh báo: còn " + string.Join(", ", parts) + " — xóa cứng sẽ gỡ liên quan.";
        }
    }
}
