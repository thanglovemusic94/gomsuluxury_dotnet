using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Menus;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public MenuInput Input { get; set; } = new();

    public IList<ParentOption> Parents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id, string? position = null)
    {
        if (id is not null)
        {
            var menu = await db.Menus.FindAsync(id);
            if (menu is null)
                return NotFound();

            Input = new MenuInput
            {
                Id = menu.Id,
                Name = menu.Name,
                Url = menu.Url,
                ParentId = menu.ParentId,
                DisplayOrder = menu.DisplayOrder,
                Position = menu.Position,
                IsActive = menu.IsActive
            };
        }
        else if (!string.IsNullOrWhiteSpace(position) && CatalogRules.MenuPositions.Contains(position))
        {
            Input.Position = position;
        }

        await LoadParentsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Name = TextHelper.Trimmed(Input.Name);
        Input.Url = TextHelper.Trimmed(Input.Url);
        Input.Position = TextHelper.Trimmed(Input.Position);
        if (Input.Name.Length is < 1 or > 100)
            ModelState.AddModelError("Input.Name", "Tên từ 1 đến 100 ký tự.");
        if (Input.Url.Length is < 1 or > 255)
            ModelState.AddModelError("Input.Url", "URL từ 1 đến 255 ký tự.");
        if (!CatalogRules.MenuPositions.Contains(Input.Position))
            ModelState.AddModelError("Input.Position", "Vị trí chỉ nhận Header hoặc Footer.");
        if (Input.ParentId == Input.Id && Input.Id != 0)
            ModelState.AddModelError("Input.ParentId", "Menu không thể là cha của chính nó.");
        if (Input.ParentId is int parentId)
        {
            var parent = await db.Menus.AsNoTracking().FirstOrDefaultAsync(menu => menu.Id == parentId);
            if (parent is null)
                ModelState.AddModelError("Input.ParentId", "Menu cha không tồn tại.");
            else if (parent.Position != Input.Position)
                ModelState.AddModelError("Input.ParentId", "Menu cha phải cùng vị trí Header hoặc Footer.");
            else if (Input.Id != 0 && await CreatesCycleAsync(Input.Id, parentId))
                ModelState.AddModelError("Input.ParentId", "Không thể chọn menu con làm cha.");
        }

        await LoadParentsAsync();
        if (!ModelState.IsValid)
            return Page();

        Menu menuEntity;
        if (Input.Id == 0)
        {
            menuEntity = new Menu();
            db.Menus.Add(menuEntity);
        }
        else
        {
            var existing = await db.Menus.FindAsync(Input.Id);
            if (existing is null)
                return NotFound();
            menuEntity = existing;
        }

        menuEntity.Name = Input.Name;
        menuEntity.Url = Input.Url;
        menuEntity.ParentId = Input.ParentId;
        menuEntity.DisplayOrder = Input.DisplayOrder;
        menuEntity.Position = Input.Position;
        menuEntity.IsActive = Input.IsActive;
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["Message"] = "Đã lưu menu.";
        return RedirectToPage("Index", new { Position = menuEntity.Position });
    }

    private async Task LoadParentsAsync()
    {
        var all = await db.Menus.AsNoTracking()
            .Where(menu => menu.Position == Input.Position)
            .OrderBy(menu => menu.DisplayOrder)
            .ToListAsync();
        var options = new List<ParentOption>();
        Walk(all, Input.Position, null, 0, options);
        Parents = options;
    }

    private void Walk(IReadOnlyList<Menu> all, string position, int? parentId, int depth, List<ParentOption> options)
    {
        foreach (var menu in all.Where(item => item.Position == position && item.ParentId == parentId))
        {
            if (menu.Id == Input.Id)
                continue;

            options.Add(new ParentOption(menu.Id, $"{new string('—', depth)} {menu.Name} ({menu.Position})".Trim()));
            Walk(all, position, menu.Id, depth + 1, options);
        }
    }

    private async Task<bool> CreatesCycleAsync(int id, int parentId)
    {
        var current = parentId;
        for (var guard = 0; guard < 50; guard++)
        {
            if (current == id)
                return true;

            var next = await db.Menus.AsNoTracking()
                .Where(menu => menu.Id == current)
                .Select(menu => menu.ParentId)
                .FirstOrDefaultAsync();
            if (next is null)
                return false;

            current = next.Value;
        }

        return true;
    }

    public record ParentOption(int Id, string Label);

    public class MenuInput
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = "/";

        public int? ParentId { get; set; }

        public int DisplayOrder { get; set; }

        public string Position { get; set; } = "Header";

        public bool IsActive { get; set; } = true;
    }
}
