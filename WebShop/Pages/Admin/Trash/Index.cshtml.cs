using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Trash;

public class IndexModel(AppDbContext db, MediaStorage media, AuditService audit) : PagedAdminPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? EntityType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public IList<TrashItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var all = await LoadAllAsync();
        if (!string.IsNullOrWhiteSpace(EntityType))
            all = all.Where(item => item.Type == EntityType).ToList();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            all = all.Where(item =>
                item.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Id.ToString() == term).ToList();
        }

        ApplyPagingTotals(all.Count);
        Items = all
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public async Task<IActionResult> OnPostRestoreAsync(
        string type, int id, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? entityType = null, string? q = null)
    {
        var (ok, error) = await RestoreOneAsync(type, id);
        if (!ok && error == "notfound")
            return NotFound();
        if (!ok && error == "badrequest")
            return BadRequest();

        TempData["Message"] = "Đã khôi phục.";
        return RedirectList(pageNumber, pageSize, entityType, q);
    }

    public async Task<IActionResult> OnPostHardDeleteAsync(
        string type, int id, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? entityType = null, string? q = null)
    {
        var (ok, error) = await HardDeleteOneAsync(type, id);
        if (!ok && error == "notfound")
            return NotFound();
        if (!ok && error == "badrequest")
            return BadRequest();
        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectList(pageNumber, pageSize, entityType, q);
        }

        TempData["Message"] = "Đã xóa vĩnh viễn.";
        return RedirectList(pageNumber, pageSize, entityType, q);
    }

    public async Task<IActionResult> OnPostBulkRestoreAsync(
        string[]? keys,
        int pageNumber = 1,
        int pageSize = AdminPaging.DefaultPageSize,
        string? entityType = null,
        string? q = null)
    {
        var pairs = ParseKeys(keys);
        if (pairs.Count == 0)
        {
            TempData["Error"] = "Chọn ít nhất một mục.";
            return RedirectList(pageNumber, pageSize, entityType, q);
        }

        var okCount = 0;
        foreach (var (type, id) in pairs)
        {
            var (ok, _) = await RestoreOneAsync(type, id);
            if (ok) okCount++;
        }

        TempData["Message"] = okCount == 0
            ? "Không khôi phục được mục nào."
            : $"Đã khôi phục {okCount} mục.";
        return RedirectList(pageNumber, pageSize, entityType, q);
    }

    public async Task<IActionResult> OnPostBulkHardDeleteAsync(
        string[]? keys,
        int pageNumber = 1,
        int pageSize = AdminPaging.DefaultPageSize,
        string? entityType = null,
        string? q = null)
    {
        var pairs = ParseKeys(keys);
        if (pairs.Count == 0)
        {
            TempData["Error"] = "Chọn ít nhất một mục.";
            return RedirectList(pageNumber, pageSize, entityType, q);
        }

        var okCount = 0;
        var errors = new List<string>();
        foreach (var (type, id) in pairs)
        {
            var (ok, error) = await HardDeleteOneAsync(type, id);
            if (ok)
                okCount++;
            else if (!string.IsNullOrWhiteSpace(error)
                     && error is not ("notfound" or "badrequest"))
                errors.Add($"#{id}: {error}");
        }

        if (okCount > 0)
            TempData["Message"] = $"Đã xóa vĩnh viễn {okCount} mục.";
        if (errors.Count > 0)
            TempData["Error"] = string.Join(" · ", errors.Take(5));
        else if (okCount == 0)
            TempData["Error"] = "Không xóa được mục nào.";

        return RedirectList(pageNumber, pageSize, entityType, q);
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/Trash", page, size, ("EntityType", EntityType), ("Q", Q));

    private IActionResult RedirectList(int pageNumber, int pageSize, string? entityType, string? q) =>
        RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            EntityType = entityType,
            Q = q
        });

    private async Task<(bool Ok, string? Error)> RestoreOneAsync(string type, int id)
    {
        switch (type)
        {
            case AuditEntities.Product:
            {
                var item = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return (false, "notfound");
                SoftDelete.Restore(item);
                var slug = item.Slug;
                SoftDelete.RestoreSlug(ref slug, item.Id);
                item.Slug = slug;
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Product, item.Id, item.Name, "Khôi phục từ thùng rác");
                return (true, null);
            }
            case AuditEntities.Post:
            {
                var item = await db.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return (false, "notfound");
                SoftDelete.Restore(item);
                var slug = item.Slug;
                SoftDelete.RestoreSlug(ref slug, item.Id);
                item.Slug = slug;
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Post, item.Id, item.Title, "Khôi phục từ thùng rác");
                return (true, null);
            }
            case AuditEntities.Page:
            {
                var item = await db.CustomPages.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return (false, "notfound");
                SoftDelete.Restore(item);
                var slug = item.Slug;
                SoftDelete.RestoreSlug(ref slug, item.Id);
                item.Slug = slug;
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Page, item.Id, item.Title, "Khôi phục từ thùng rác");
                return (true, null);
            }
            case AuditEntities.Media:
            {
                var ok = await media.RestoreAsync(id);
                if (!ok) return (false, "notfound");
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Media, id, null, "Khôi phục từ thùng rác");
                return (true, null);
            }
            default:
                return (false, "badrequest");
        }
    }

    private async Task<(bool Ok, string? Error)> HardDeleteOneAsync(string type, int id)
    {
        switch (type)
        {
            case AuditEntities.Product:
            {
                var item = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return (false, "notfound");

                var blockers = await ProductDeleteGuard.GetBlockersAsync(db, id);
                if (blockers.IsBlocked)
                    await ProductDeleteGuard.ClearRelatedAsync(db, id);

                var title = item.Name;
                var note = blockers.IsBlocked
                    ? $"Xóa vĩnh viễn (đã gỡ {blockers.OrderLines} dòng đơn, {blockers.Reviews} đánh giá)"
                    : "Xóa vĩnh viễn";
                db.Products.Remove(item);
                var error = await DbSave.TryDeleteAsync(db);
                if (error is not null)
                    return (false, $"#{id} «{title}»: {error}");

                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Product, id, title, note);
                return (true, null);
            }
            case AuditEntities.Post:
            {
                var item = await db.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return (false, "notfound");
                var title = item.Title;
                db.Posts.Remove(item);
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Post, id, title, "Xóa vĩnh viễn");
                return (true, null);
            }
            case AuditEntities.Page:
            {
                var item = await db.CustomPages.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return (false, "notfound");
                var title = item.Title;
                db.CustomPages.Remove(item);
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Page, id, title, "Xóa vĩnh viễn");
                return (true, null);
            }
            case AuditEntities.Media:
            {
                var ok = await media.HardDeleteAsync(id);
                if (!ok) return (false, "notfound");
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Media, id, null, "Xóa vĩnh viễn");
                return (true, null);
            }
            default:
                return (false, "badrequest");
        }
    }

    private static List<(string Type, int Id)> ParseKeys(string[]? keys)
    {
        var list = new List<(string, int)>();
        if (keys is null || keys.Length == 0)
            return list;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in keys)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var id) || id <= 0)
                continue;
            var type = parts[0];
            if (type is not (AuditEntities.Product or AuditEntities.Post or AuditEntities.Page or AuditEntities.Media))
                continue;
            var token = type + "|" + id;
            if (!seen.Add(token))
                continue;
            list.Add((type, id));
        }

        return list;
    }

    private async Task<List<TrashItem>> LoadAllAsync()
    {
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.IsDeleted)
            .Select(item => new TrashItem(AuditEntities.Product, item.Id, item.Name, item.DeletedAt, item.DeletedBy))
            .ToListAsync();
        var posts = await db.Posts.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.IsDeleted)
            .Select(item => new TrashItem(AuditEntities.Post, item.Id, item.Title, item.DeletedAt, item.DeletedBy))
            .ToListAsync();
        var pages = await db.CustomPages.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.IsDeleted)
            .Select(item => new TrashItem(AuditEntities.Page, item.Id, item.Title, item.DeletedAt, item.DeletedBy))
            .ToListAsync();
        var mediaItems = await db.MediaAssets.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.IsDeleted)
            .Select(item => new TrashItem(AuditEntities.Media, item.Id, item.FileName, item.DeletedAt, item.DeletedBy))
            .ToListAsync();

        return products.Concat(posts).Concat(pages).Concat(mediaItems)
            .OrderByDescending(item => item.DeletedAt ?? DateTime.MinValue)
            .ToList();
    }

    public sealed record TrashItem(string Type, int Id, string Title, DateTime? DeletedAt, string? DeletedBy);
}
