using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Gắn ảnh gallery (khác ảnh chính) để card shop đổi ảnh khi hover.
/// </summary>
public static class ProductCardMedia
{
    public static async Task AttachHoverImagesAsync(
        AppDbContext db,
        IEnumerable<Product> products,
        CancellationToken ct = default)
    {
        var list = products as IList<Product> ?? products.ToList();
        if (list.Count == 0)
            return;

        var ids = list.Select(item => item.Id).Distinct().ToList();
        var rows = await db.ProductImages.AsNoTracking()
            .Where(item => ids.Contains(item.ProductId) && item.ImageUrl != "")
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .Select(item => new { item.ProductId, item.ImageUrl })
            .ToListAsync(ct);

        var mains = list.ToDictionary(item => item.Id, item => FileKey(item.ImageUrl));
        foreach (var group in rows.GroupBy(item => item.ProductId))
        {
            if (!mains.TryGetValue(group.Key, out var mainKey))
                continue;
            var alt = group.FirstOrDefault(item => FileKey(item.ImageUrl) != mainKey);
            if (alt is null)
                continue;
            foreach (var product in list)
            {
                if (product.Id == group.Key)
                    product.CardHoverImageUrl = alt.ImageUrl;
            }
        }
    }

    private static string FileKey(string? url)
    {
        var path = (url ?? "").Split('?', '#')[0].Trim();
        var name = Path.GetFileNameWithoutExtension(path);
        return name.ToLowerInvariant();
    }
}
