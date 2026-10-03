namespace WebShop.Infrastructure;

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string SoftDelete = "SoftDelete";
    public const string Restore = "Restore";
    public const string HardDelete = "HardDelete";

    public static string Label(string? action) => action switch
    {
        Create => "Tạo",
        Update => "Sửa",
        SoftDelete => "Xóa mềm",
        Restore => "Khôi phục",
        HardDelete => "Xóa cứng",
        _ => action ?? string.Empty
    };
}

public static class AuditEntities
{
    public const string Product = "Product";
    public const string Post = "Post";
    public const string Page = "CustomPage";
    public const string Media = "Media";

    public static string Label(string? type) => type switch
    {
        Product => "Sản phẩm",
        Post => "Bài viết",
        Page => "Trang tĩnh",
        Media => "Media",
        _ => type ?? string.Empty
    };
}
