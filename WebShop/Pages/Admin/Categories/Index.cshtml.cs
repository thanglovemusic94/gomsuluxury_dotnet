using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Categories;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<Category> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await db.Categories.AsNoTracking()
            .Include(category => category.Parent)
            .OrderBy(category => category.Type)
            .ThenBy(category => category.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleVisibleAsync(int id)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null)
            return NotFound();

        category.IsVisible = !category.IsVisible;
        if (!await DbSave.TrySaveAsync(db, ModelState))
        {
            TempData["Error"] = "Không đổi được trạng thái hiển thị.";
            return RedirectToPage();
        }

        TempData["Message"] = category.IsVisible
            ? $"Đã hiện danh mục «{category.Name}» trên cửa hàng."
            : $"Đã ẩn danh mục «{category.Name}» — sản phẩm chỉ thuộc mục này cũng không hiện trên shop.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null)
            return NotFound();

        db.Categories.Remove(category);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa danh mục.";
        return RedirectToPage();
    }
}
