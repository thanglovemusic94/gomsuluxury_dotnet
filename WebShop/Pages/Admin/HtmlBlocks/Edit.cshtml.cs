using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.HtmlBlocks;

public class EditModel(AppDbContext db, HtmlBlockService blocks) : PageModel
{
    [BindProperty]
    public BlockInput Input { get; set; } = new() { IsActive = true };

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
            return Page();

        var item = await db.HtmlBlocks.AsNoTracking().FirstOrDefaultAsync(block => block.Id == id);
        if (item is null)
            return NotFound();

        Input = BlockInput.From(item);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Title = TextHelper.Trimmed(Input.Title);
        Input.Content = TextHelper.Trimmed(Input.Content);
        Input.Note = TextHelper.Clean(Input.Note);
        var rawKey = TextHelper.Trimmed(Input.Key);
        if (string.IsNullOrWhiteSpace(rawKey))
            ModelState.AddModelError("Input.Key", "Nhập key (vd. home-promo).");
        var key = HtmlBlockService.NormalizeKey(rawKey);
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            ModelState.AddModelError("Input.Key", "Key từ 1 đến 100 ký tự (a-z, 0-9, -).");
        if (Input.Title.Length is < 1 or > 200)
            ModelState.AddModelError("Input.Title", "Tiêu đề từ 1 đến 200 ký tự.");
        if (string.IsNullOrWhiteSpace(Input.Content))
            ModelState.AddModelError("Input.Content", "Nhập nội dung HTML.");
        if (await db.HtmlBlocks.AnyAsync(block => block.Key == key && block.Id != Input.Id))
            ModelState.AddModelError("Input.Key", "Key đã tồn tại.");
        if (!ModelState.IsValid)
            return Page();

        HtmlBlock entity;
        string? oldKey = null;
        if (Input.Id == 0)
        {
            entity = new HtmlBlock();
            db.HtmlBlocks.Add(entity);
        }
        else
        {
            var existing = await db.HtmlBlocks.FirstOrDefaultAsync(block => block.Id == Input.Id);
            if (existing is null)
                return NotFound();
            entity = existing;
            oldKey = existing.Key;
        }

        entity.Key = key;
        entity.Title = Input.Title;
        entity.Content = Input.Content;
        entity.Note = Input.Note;
        entity.IsActive = Input.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        blocks.Invalidate(oldKey);
        blocks.Invalidate(key);
        TempData["Message"] = "Đã lưu block.";
        return RedirectToPage("Index");
    }

    public class BlockInput
    {
        public int Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string? Note { get; set; }

        public bool IsActive { get; set; } = true;

        public static BlockInput From(HtmlBlock block) => new()
        {
            Id = block.Id,
            Key = block.Key,
            Title = block.Title,
            Content = block.Content,
            Note = block.Note,
            IsActive = block.IsActive
        };
    }
}
