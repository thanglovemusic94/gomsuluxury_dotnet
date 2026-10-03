using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.StaticPages;

public class EditModel(AppDbContext db, AuditService audit) : PageModel
{
    [BindProperty]
    public PageInput Input { get; set; } = new() { IsPublished = true };

    [BindProperty]
    public SeoInput Seo { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
            return Page();

        var page = await db.CustomPages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (page is null)
            return NotFound();

        Input = PageInput.From(page);
        Seo = SeoInput.From(page);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Title = TextHelper.Trimmed(Input.Title);
        Input.Content = TextHelper.Trimmed(Input.Content);
        if (Input.Title.Length is < 1 or > 250)
            ModelState.AddModelError("Input.Title", "Tiêu đề từ 1 đến 250 ký tự.");
        if (string.IsNullOrWhiteSpace(Input.Content))
            ModelState.AddModelError("Input.Content", "Nhập nội dung.");
        Seo.Validate(ModelState);

        var slug = PagePaths.NormalizeSlug(TextHelper.Slug(Input.Slug, Input.Title, keepSlash: false));
        if (string.IsNullOrEmpty(slug))
            ModelState.AddModelError("Input.Slug", "Slug không hợp lệ.");
        else if (PagePaths.IsReserved(slug))
            ModelState.AddModelError("Input.Slug", "Slug trùng với đường dẫn hệ thống.");
        if (slug.Length > 255)
            ModelState.AddModelError("Input.Slug", "Slug tối đa 255 ký tự.");
        if (await db.CustomPages.AnyAsync(page => page.Slug == slug && page.Id != Input.Id))
            ModelState.AddModelError("Input.Slug", "Slug đã tồn tại.");
        if (!ModelState.IsValid)
            return Page();

        CustomPage pageEntity;
        var isCreate = Input.Id == 0;
        Dictionary<string, string?>? before = null;
        if (isCreate)
        {
            pageEntity = new CustomPage();
            db.CustomPages.Add(pageEntity);
        }
        else
        {
            var existing = await db.CustomPages.FirstOrDefaultAsync(page => page.Id == Input.Id);
            if (existing is null)
                return NotFound();
            pageEntity = existing;
            before = SnapshotPage(pageEntity);
        }

        pageEntity.Title = Input.Title;
        pageEntity.Slug = slug;
        pageEntity.Content = Input.Content;
        pageEntity.IsPublished = Input.IsPublished;
        Seo.Apply(pageEntity);
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        if (isCreate)
            await audit.LogAsync(AuditActions.Create, AuditEntities.Page, pageEntity.Id, pageEntity.Title, "Tạo trang tĩnh mới");
        else if (before is not null)
        {
            var changes = AuditService.Diff(before, SnapshotPage(pageEntity));
            if (changes.Count > 0)
                await audit.LogAsync(AuditActions.Update, AuditEntities.Page, pageEntity.Id, pageEntity.Title,
                    $"Sửa {changes.Count} trường", string.Join("\n", changes));
        }

        TempData["Message"] = "Đã lưu trang.";
        return RedirectToPage("Index");
    }

    private static Dictionary<string, string?> SnapshotPage(CustomPage page) => new()
    {
        ["Title"] = page.Title,
        ["Slug"] = page.Slug,
        ["Content"] = page.Content,
        ["IsPublished"] = page.IsPublished.ToString()
    };

    public class PageInput
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool IsPublished { get; set; } = true;

        public static PageInput From(CustomPage page) => new()
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            Content = page.Content,
            IsPublished = page.IsPublished
        };
    }
}
