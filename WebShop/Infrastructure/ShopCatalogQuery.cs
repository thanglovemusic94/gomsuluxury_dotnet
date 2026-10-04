using WebShop.Models;

namespace WebShop.Infrastructure;

public static class ShopCatalogQuery
{
    /// <summary>
    /// Sản phẩm hiện trên shop: đang bật và thuộc ít nhất một danh mục Product đang hiện
    /// (hoặc Category chính đang hiện nếu chưa gắn ProductCategories).
    /// </summary>
    public static IQueryable<Product> WhereListedOnShop(this IQueryable<Product> query) =>
        query.Where(product => product.IsVisible && (
            product.ProductCategories.Any(link =>
                link.Category.Type == "Product" && link.Category.IsVisible)
            || (!product.ProductCategories.Any(link => link.Category.Type == "Product")
                && product.Category.IsVisible)));
}
