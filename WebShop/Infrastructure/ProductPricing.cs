using WebShop.Models;

namespace WebShop.Infrastructure;

public static class ProductPricing
{
    public static decimal UnitPrice(Product product) => product.DiscountPrice ?? product.Price;

    /// <summary>
    /// Ẩn giá + CTA liên hệ trên shop: tick <see cref="Product.HidePrice"/>
    /// hoặc giá hiệu lực ≤ 0.
    /// </summary>
    public static bool ShowContactOnly(Product product) =>
        product.HidePrice || UnitPrice(product) <= 0;
}
