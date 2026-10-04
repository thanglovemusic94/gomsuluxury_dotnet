using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Categories;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public CategoryInput Input { get; set; } = new();

    public IList<ParentOption> Parents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is not null)
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null)
                return NotFound();

            Input = new CategoryInput
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Type = category.Type,
                ParentId = category.ParentId,
                IsVisible = category.IsVisible
            };
        }

        await LoadParentsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Name = TextHelper.Trimmed(Input.Name);
        Input.Type = TextHelper.Trimmed(Input.Type);
        if (Input.Name.Length is < 1 or > 100)
            ModelState.AddModelError("Input.Name", "Tên từ 1 đến 100 ký tự.");
        if (!CatalogRules.CategoryTypes.Contains(Input.Type))
            ModelState.AddModelError("Input.Type", "Loại chỉ nhận Product hoặc Blog.");

        var slug = TextHelper.Slug(Input.Slug, Input.Name);
        if (slug.Length > 150)
            ModelState.AddModelError("Input.Slug", "Slug tối đa 150 ký tự.");
        if (await db.Categories.AnyAsync(category => category.Slug == slug && category.Id != Input.Id))
            ModelState.AddModelError("Input.Slug", "Slug đã tồn tại.");
        if (Input.ParentId == Input.Id && Input.Id != 0)
            ModelState.AddModelError("Input.ParentId", "Danh mục không thể là cha của chính nó.");
        if (Input.ParentId is int parentId)
        {
            var parent = await db.Categories.AsNoTracking().FirstOrDefaultAsync(category => category.Id == parentId);
            if (parent is null)
                ModelState.AddModelError("Input.ParentId", "Danh mục cha không tồn tại.");
            else if (parent.Type != Input.Type)
                ModelState.AddModelError("Input.ParentId", "Danh mục cha phải cùng loại.");
            else if (Input.Id != 0 && await CreatesCycleAsync(Input.Id, parentId))
                ModelState.AddModelError("Input.ParentId", "Không thể chọn danh mục con làm cha.");
        }

        await LoadParentsAsync();
        if (!ModelState.IsValid)
            return Page();

        Category categoryEntity;
        if (Input.Id == 0)
        {
            categoryEntity = new Category();
            db.Categories.Add(categoryEntity);
        }
        else
        {
            var existing = await db.Categories.FindAsync(Input.Id);
            if (existing is null)
                return NotFound();
            categoryEntity = existing;
        }

        categoryEntity.Name = Input.Name;
        categoryEntity.Slug = slug;
        categoryEntity.Type = Input.Type;
        categoryEntity.ParentId = Input.ParentId;
        categoryEntity.IsVisible = Input.IsVisible;
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["Message"] = "Đã lưu danh mục.";
        return RedirectToPage("Index");
    }

    private async Task LoadParentsAsync()
    {
        var all = await db.Categories.AsNoTracking().OrderBy(category => category.Name).ToListAsync();
        var options = new List<ParentOption>();
        Walk(all, null, 0, options);
        Parents = options;
    }

    private void Walk(IReadOnlyList<Category> all, int? parentId, int depth, List<ParentOption> options)
    {
        foreach (var category in all.Where(item => item.ParentId == parentId))
        {
            if (category.Id == Input.Id)
                continue;

            options.Add(new ParentOption(category.Id, $"{new string('—', depth)} {category.Name} ({category.Type})".Trim()));
            Walk(all, category.Id, depth + 1, options);
        }
    }

    private async Task<bool> CreatesCycleAsync(int id, int parentId)
    {
        var current = parentId;
        for (var guard = 0; guard < 50; guard++)
        {
            if (current == id)
                return true;

            var next = await db.Categories.AsNoTracking()
                .Where(category => category.Id == current)
                .Select(category => category.ParentId)
                .FirstOrDefaultAsync();
            if (next is null)
                return false;

            current = next.Value;
        }

        return true;
    }

    public record ParentOption(int Id, string Label);

    public class CategoryInput
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string Type { get; set; } = "Product";

        public int? ParentId { get; set; }

        public bool IsVisible { get; set; } = true;
    }
}
