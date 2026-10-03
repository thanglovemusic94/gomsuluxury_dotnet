using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Menus;

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Position { get; set; } = "Header";

    public IList<Menu> Items { get; private set; } = [];

    public IReadOnlyList<MenuTreeNode> Tree { get; private set; } = [];

    public async Task OnGetAsync()
    {
        if (!CatalogRules.MenuPositions.Contains(Position))
            Position = "Header";

        Items = await db.Menus.AsNoTracking()
            .Where(menu => menu.Position == Position)
            .OrderBy(menu => menu.DisplayOrder)
            .ThenBy(menu => menu.Name)
            .ToListAsync();

        Tree = BuildTree(Items, null);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? position = null)
    {
        var menu = await db.Menus.FindAsync(id);
        if (menu is null)
            return NotFound();

        var pos = position ?? menu.Position;
        var hasChildren = await db.Menus.AnyAsync(item => item.ParentId == id);
        if (hasChildren)
        {
            TempData["Error"] = "Không xóa được: hãy kéo các mục con ra ngoài hoặc xóa chúng trước.";
            return RedirectToPage(new { Position = pos });
        }

        db.Menus.Remove(menu);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa menu.";
        return RedirectToPage(new { Position = pos });
    }

    public async Task<IActionResult> OnPostReorderAsync([FromBody] List<ReorderItem>? items)
    {
        if (items is null || items.Count == 0)
            return new JsonResult(new { ok = false, error = "Không có dữ liệu." });

        var ids = items.Select(item => item.Id).Distinct().ToList();
        var menus = await db.Menus.Where(menu => ids.Contains(menu.Id)).ToListAsync();
        if (menus.Count != ids.Count)
            return new JsonResult(new { ok = false, error = "Có menu không tồn tại." });

        var byId = menus.ToDictionary(menu => menu.Id);
        var position = byId[ids[0]].Position;
        if (menus.Any(menu => menu.Position != position))
            return new JsonResult(new { ok = false, error = "Không thể trộn Header và Footer." });

        // Validate parents / cycles before applying.
        foreach (var item in items)
        {
            if (item.ParentId is int parentId)
            {
                if (parentId == item.Id)
                    return new JsonResult(new { ok = false, error = "Menu không thể là cha của chính nó." });
                if (!byId.TryGetValue(parentId, out var parent) || parent.Position != position)
                    return new JsonResult(new { ok = false, error = "Menu cha không hợp lệ." });
            }
        }

        if (HasCycle(items))
            return new JsonResult(new { ok = false, error = "Cấu trúc menu tạo vòng lặp." });

        if (MaxDepth(items) > 3)
            return new JsonResult(new { ok = false, error = "Chỉ hỗ trợ tối đa 3 cấp menu." });

        foreach (var item in items)
        {
            var menu = byId[item.Id];
            menu.ParentId = item.ParentId;
            menu.DisplayOrder = item.DisplayOrder;
        }

        await db.SaveChangesAsync();
        return new JsonResult(new { ok = true });
    }

    private static List<MenuTreeNode> BuildTree(IEnumerable<Menu> all, int? parentId)
    {
        var list = all as IList<Menu> ?? all.ToList();
        return list.Where(menu => menu.ParentId == parentId)
            .OrderBy(menu => menu.DisplayOrder)
            .ThenBy(menu => menu.Name)
            .Select(menu => new MenuTreeNode(menu, BuildTree(list, menu.Id)))
            .ToList();
    }

    private static bool HasCycle(IReadOnlyList<ReorderItem> items)
    {
        var parentOf = items.ToDictionary(item => item.Id, item => item.ParentId);
        foreach (var id in parentOf.Keys)
        {
            var current = parentOf[id];
            for (var guard = 0; guard < items.Count + 2; guard++)
            {
                if (current is null) break;
                if (current == id) return true;
                if (!parentOf.TryGetValue(current.Value, out var next)) break;
                current = next;
            }
        }

        return false;
    }

    private static int MaxDepth(IReadOnlyList<ReorderItem> items)
    {
        var children = items.GroupBy(item => item.ParentId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());

        int Depth(int? parentId, int level)
        {
            if (!children.TryGetValue(parentId, out var kids) || kids.Count == 0)
                return level;
            return kids.Max(id => Depth(id, level + 1));
        }

        return Depth(null, 0);
    }

    public sealed record MenuTreeNode(Menu Menu, IReadOnlyList<MenuTreeNode> Children);

    public sealed class ReorderItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("parentId")]
        public int? ParentId { get; set; }

        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }
    }
}
