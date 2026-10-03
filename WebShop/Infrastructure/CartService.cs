using System.Text.Json;

namespace WebShop.Infrastructure;

public class CartLine
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
}

public class CartService(IHttpContextAccessor http)
{
    private const string Key = "WebShop.Cart";

    public IReadOnlyList<CartLine> Get()
    {
        if (http.HttpContext?.Request.Cookies.TryGetValue(Key, out var json) != true || string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<CartLine>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public int Count => Get().Sum(line => line.Quantity);

    public void Add(CartLine line)
    {
        var items = Get().ToList();
        var existing = items.FirstOrDefault(item => item.ProductId == line.ProductId);
        if (existing is null)
            items.Add(line);
        else
            existing.Quantity += line.Quantity;

        Save(items);
    }

    public void SetQuantity(int productId, int quantity)
    {
        var items = Get().ToList();
        var existing = items.FirstOrDefault(item => item.ProductId == productId);
        if (existing is null)
            return;

        if (quantity <= 0)
            items.Remove(existing);
        else
            existing.Quantity = quantity;

        Save(items);
    }

    public void Remove(int productId) => SetQuantity(productId, 0);

    public void Clear() => Save([]);

    private void Save(List<CartLine> items)
    {
        var context = http.HttpContext ?? throw new InvalidOperationException("Không có HttpContext để lưu giỏ hàng.");
        var options = new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            MaxAge = TimeSpan.FromDays(7),
            Path = "/"
        };

        if (items.Count == 0)
            context.Response.Cookies.Delete(Key, options);
        else
            context.Response.Cookies.Append(Key, JsonSerializer.Serialize(items), options);
    }
}
