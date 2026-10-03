using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Roles;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<Role> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await db.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is null)
            return NotFound();

        db.Roles.Remove(role);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa vai trò.";
        return RedirectToPage();
    }
}