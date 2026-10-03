using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Users;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public UserInput Input { get; set; } = new();

    public IList<Role> Roles { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadAsync();
        if (id is null)
        {
            Input.IsActive = true;
            return Page();
        }

        var user = await db.Users.Include(item => item.Roles).FirstOrDefaultAsync(item => item.Id == id);
        if (user is null)
            return NotFound();

        Input = new UserInput
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            RoleIds = user.Roles.Select(role => role.Id).ToList()
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        Input.Username = TextHelper.Trimmed(Input.Username);
        Input.Email = TextHelper.Trimmed(Input.Email);
        Input.RoleIds ??= [];

        if (Input.Username.Length is < 1 or > 50)
            ModelState.AddModelError("Input.Username", "Tên đăng nhập từ 1 đến 50 ký tự.");
        if (Input.Email.Length is < 3 or > 150 || !Input.Email.Contains('@'))
            ModelState.AddModelError("Input.Email", "Email chưa hợp lệ.");
        if (Input.FullName?.Length > 100)
            ModelState.AddModelError("Input.FullName", "Họ tên tối đa 100 ký tự.");
        if (Input.PhoneNumber?.Length > 15)
            ModelState.AddModelError("Input.PhoneNumber", "Số điện thoại tối đa 15 ký tự.");
        if (Input.Id == 0 && string.IsNullOrWhiteSpace(Input.Password))
            ModelState.AddModelError("Input.Password", "Nhập mật khẩu khi tạo tài khoản.");
        if (!string.IsNullOrWhiteSpace(Input.Password) && Input.Password.Trim().Length < 6)
            ModelState.AddModelError("Input.Password", "Mật khẩu tối thiểu 6 ký tự.");
        if (await db.Users.AnyAsync(user => user.Username == Input.Username && user.Id != Input.Id))
            ModelState.AddModelError("Input.Username", "Tên đăng nhập đã tồn tại.");
        if (await db.Users.AnyAsync(user => user.Email == Input.Email && user.Id != Input.Id))
            ModelState.AddModelError("Input.Email", "Email đã tồn tại.");
        if (Input.RoleIds.Count > 0 && await db.Roles.CountAsync(role => Input.RoleIds.Contains(role.Id)) != Input.RoleIds.Count)
            ModelState.AddModelError(string.Empty, "Có vai trò không còn tồn tại.");
        if (!ModelState.IsValid)
            return Page();

        User userEntity;
        if (Input.Id == 0)
        {
            userEntity = new Models.User();
            db.Users.Add(userEntity);
        }
        else
        {
            var existing = await db.Users.Include(user => user.UserRoles).FirstOrDefaultAsync(user => user.Id == Input.Id);
            if (existing is null)
                return NotFound();
            userEntity = existing;
        }

        userEntity.Username = Input.Username;
        userEntity.Email = Input.Email;
        userEntity.FullName = TextHelper.Clean(Input.FullName);
        userEntity.PhoneNumber = TextHelper.Clean(Input.PhoneNumber);
        userEntity.IsActive = Input.IsActive;
        if (!string.IsNullOrWhiteSpace(Input.Password))
            userEntity.PasswordHash = TextHelper.HashPassword(Input.Password.Trim());

        if (Input.Id == 0)
        {
            userEntity.UserRoles = Input.RoleIds.Select(roleId => new UserRole { RoleId = roleId }).ToList();
        }
        else
        {
            var removed = userEntity.UserRoles.Where(link => !Input.RoleIds.Contains(link.RoleId)).ToList();
            db.UserRoles.RemoveRange(removed);
            foreach (var roleId in Input.RoleIds.Except(userEntity.UserRoles.Select(link => link.RoleId)))
                db.UserRoles.Add(new UserRole { UserId = userEntity.Id, RoleId = roleId });
        }

        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["Message"] = "Đã lưu thành viên.";
        return RedirectToPage("Index");
    }

    private async Task LoadAsync()
    {
        Roles = await db.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync();
    }

    public class UserInput
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? Password { get; set; }

        public string? FullName { get; set; }

        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public List<int> RoleIds { get; set; } = [];
    }
}
