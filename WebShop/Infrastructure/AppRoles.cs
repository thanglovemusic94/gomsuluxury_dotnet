namespace WebShop.Infrastructure;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Viewer = "Viewer";
    public const string Customer = "Customer";

    /// <summary>Can enter the admin area (including read-only Viewer).</summary>
    public const string AdminArea = Admin + "," + Staff + "," + Viewer;

    /// <summary>Admin menus Staff cannot see; Viewer may browse read-only.</summary>
    public const string AdminOrViewer = Admin + "," + Viewer;

    public static bool IsAdmin(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Admin);

    public static bool IsViewer(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Viewer);

    /// <summary>Viewer without Admin — browse Admin, no writes.</summary>
    public static bool IsReadOnlyAdmin(System.Security.Claims.ClaimsPrincipal user) =>
        IsViewer(user) && !IsAdmin(user);

    public static bool CanWriteAdmin(System.Security.Claims.ClaimsPrincipal user) =>
        IsAdmin(user) || user.IsInRole(Staff);

    public static bool CanSeeAdminManagement(System.Security.Claims.ClaimsPrincipal user) =>
        IsAdmin(user) || IsViewer(user);

    public static bool IsStaffArea(System.Security.Claims.ClaimsPrincipal user) =>
        IsAdmin(user) || user.IsInRole(Staff) || IsViewer(user);
}
