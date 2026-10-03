using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Settings;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public SettingInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
            return Page();

        var setting = await db.SystemSettings.FindAsync(id);
        if (setting is null)
            return NotFound();

        Input = new SettingInput
        {
            Id = setting.Id,
            Key = setting.Key,
            Value = setting.Value,
            Description = setting.Description
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Key = TextHelper.Trimmed(Input.Key);
        if (Input.Key.Length is < 1 or > 100)
            ModelState.AddModelError("Input.Key", "Khóa từ 1 đến 100 ký tự.");
        if (Input.Description?.Length > 255)
            ModelState.AddModelError("Input.Description", "Mô tả tối đa 255 ký tự.");
        if (await db.SystemSettings.AnyAsync(setting => setting.Key == Input.Key && setting.Id != Input.Id))
            ModelState.AddModelError("Input.Key", "Khóa đã tồn tại.");
        if (!ModelState.IsValid)
            return Page();

        SystemSetting settingEntity;
        if (Input.Id == 0)
        {
            settingEntity = new SystemSetting();
            db.SystemSettings.Add(settingEntity);
        }
        else
        {
            var existing = await db.SystemSettings.FindAsync(Input.Id);
            if (existing is null)
                return NotFound();
            settingEntity = existing;
        }

        settingEntity.Key = Input.Key;
        settingEntity.Value = Input.Value;
        settingEntity.Description = TextHelper.Clean(Input.Description);
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["Message"] = "Đã lưu cấu hình.";
        return RedirectToPage("Index");
    }

    public class SettingInput
    {
        public int Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }

        public string? Description { get; set; }
    }
}
