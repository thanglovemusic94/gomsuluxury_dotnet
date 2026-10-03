namespace WebShop.Models;

public class Menu
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public int DisplayOrder { get; set; }

    public string Position { get; set; } = "Header";

    public bool IsActive { get; set; } = true;

    public Menu? Parent { get; set; }

    public List<Menu> Children { get; set; } = [];
}
