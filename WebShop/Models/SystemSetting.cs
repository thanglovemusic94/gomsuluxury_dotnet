namespace WebShop.Models;

public class SystemSetting
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Description { get; set; }
}
