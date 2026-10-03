using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

public class ShopSlide
{
    public string ImageUrl { get; init; } = string.Empty;

    public string LinkUrl { get; init; } = "/san-pham";

    public string Alt { get; init; } = string.Empty;
}

public static class ShopSlides
{
    public static IReadOnlyList<ShopSlide> Default { get; } =
    [
        new ShopSlide
        {
            ImageUrl = "https://gomsuluxury.vn/wp-content/uploads/2025/12/Nhat-ma-thien-ha-thuy-15.png",
            LinkUrl = "/san-pham",
            Alt = "Nhất mã thiên hạ thủy"
        },
        new ShopSlide
        {
            ImageUrl = "https://gomsuluxury.vn/wp-content/uploads/2024/02/wweb.jpg",
            LinkUrl = "/danh-muc/am-chen-bat-trang",
            Alt = "Gốm sứ Bát Tràng"
        }
    ];

    public static async Task<IReadOnlyList<ShopSlide>> LoadAsync(AppDbContext db)
    {
        var raw = await db.SystemSettings.AsNoTracking()
            .Where(item => item.Key == RemoteImageImport.SlidesKey)
            .Select(item => item.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(raw))
            return Default;

        try
        {
            var slides = JsonSerializer.Deserialize<List<ShopSlide>>(raw);
            return slides is { Count: > 0 } ? slides : Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }
}
