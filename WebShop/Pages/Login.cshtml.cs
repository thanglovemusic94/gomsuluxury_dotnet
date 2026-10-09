using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;

namespace WebShop.Pages;

public class LoginModel(AppDbContext db) : PageModel
{
    public const string RememberUserCookie = "gomsuluxury.RememberUser";

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public bool Popup { get; set; }

    public IActionResult OnGet(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(Local(returnUrl));

        ReturnUrl = returnUrl;
        if (Request.Cookies.TryGetValue(RememberUserCookie, out var savedUser) &&
            !string.IsNullOrWhiteSpace(savedUser))
        {
            Input.Username = savedUser;
            Input.RememberMe = true;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var username = TextHelper.Trimmed(Input.Username);
        var user = await db.Users
            .Include(item => item.Roles)
            .FirstOrDefaultAsync(item => item.Username == username);

        if (user is null || !user.IsActive || !TextHelper.VerifyPassword(Input.Password, user.PasswordHash))
        {
            const string failure = "Sai tên đăng nhập hoặc mật khẩu, hoặc tài khoản đang bị khóa.";
            if (Popup)
            {
                TempData["LoginError"] = failure;
                return Redirect(ShopReturn());
            }

            ModelState.AddModelError(string.Empty, failure);
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email)
        };
        if (!string.IsNullOrWhiteSpace(user.FullName))
            claims.Add(new Claim("FullName", user.FullName));
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.Name)));

        var authProps = new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe,
            AllowRefresh = true
        };
        if (Input.RememberMe)
            authProps.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProps);

        SetRememberUserCookie(user.Username, Input.RememberMe);

        return Redirect(Local(ReturnUrl, principal));
    }

    private void SetRememberUserCookie(string username, bool remember)
    {
        if (!remember)
        {
            Response.Cookies.Delete(RememberUserCookie);
            return;
        }

        Response.Cookies.Append(RememberUserCookie, username, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    private string Local(string? returnUrl, ClaimsPrincipal? principal = null)
    {
        principal ??= User;
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            if (returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
                && !AppRoles.IsStaffArea(principal))
                return "/";
            return returnUrl;
        }

        return AppRoles.IsStaffArea(principal) ? "/Admin" : "/";
    }

    private string ShopReturn()
    {
        var target = Local(ReturnUrl);
        return target.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ? "/" : target;
    }

    public class LoginInput
    {
        public string Username { get; set; } = string.Empty;

        public string? Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
