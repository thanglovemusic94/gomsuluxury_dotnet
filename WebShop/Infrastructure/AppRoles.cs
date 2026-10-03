namespace WebShop.Infrastructure;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";

    /// <summary>Can enter the admin area.</summary>
    public const string AdminOrStaff = Admin + "," + Staff;

    public static bool IsAdmin(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Admin);

    public static bool IsStaffArea(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Admin) || user.IsInRole(Staff);
}
