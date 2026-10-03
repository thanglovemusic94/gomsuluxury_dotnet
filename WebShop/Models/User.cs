namespace WebShop.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Role> Roles { get; set; } = [];

    public List<UserRole> UserRoles { get; set; } = [];

    public List<Order> Orders { get; set; } = [];

    public List<Post> Posts { get; set; } = [];

    public List<ProductReview> Reviews { get; set; } = [];
}
