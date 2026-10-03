using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Roles;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public RoleInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
            return Page();

        var role = await db.Roles.FindAsync(id);
        if (role is null)
            return NotFound();

        Input = new RoleInput { Id = role.Id, Name = role.Name, Description = role.Description };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Name = TextHelper.Trimmed(Input.Name);
        if (Input.Name.Length is < 1 or > 50)
            ModelState.AddModelError("Input.Name", "Tên vai trò từ 1 đến 50 ký tự.");
        if (Input.Description?.Length > 255)
            ModelState.AddModelError("Input.Description", "Mô tả tối đa 255 ký tự.");
        if (await db.Roles.AnyAsync(role => role.Name == Input.Name && role.Id != Input.Id))
            ModelState.AddModelError("Input.Name", "Tên vai trò đã tồn tại.");
        if (!ModelState.IsValid)
            return Page();

        Role roleEntity;
        if (Input.Id == 0)
        {
            roleEntity = new Role();
            db.Roles.Add(roleEntity);
        }
        else
        {
            var existing = await db.Roles.FindAsync(Input.Id);
            if (existing is null)
                return NotFound();
            roleEntity = existing;
        }

        roleEntity.Name = Input.Name;
        roleEntity.Description = TextHelper.Clean(Input.Description);
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["Message"] = "Đã lưu vai trò.";
        return RedirectToPage("Index");
    }

    public class RoleInput
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
