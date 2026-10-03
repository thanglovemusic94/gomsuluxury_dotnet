namespace WebShop.Models;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public Category? Parent { get; set; }

    public List<Category> Children { get; set; } = [];

    public List<Product> Products { get; set; } = [];

    public List<ProductCategory> ProductCategories { get; set; } = [];

    public List<Post> Posts { get; set; } = [];
}
