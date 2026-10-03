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
        switch (type)
        {
            case AuditEntities.Product:
            {
                var item = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return NotFound();
                SoftDelete.Restore(item);
                var slug = item.Slug;
                SoftDelete.RestoreSlug(ref slug, item.Id);
                item.Slug = slug;
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Product, item.Id, item.Name, "Khôi phục từ thùng rác");
                break;
            }
            case AuditEntities.Post:
            {
                var item = await db.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return NotFound();
                SoftDelete.Restore(item);
                var slug = item.Slug;
                SoftDelete.RestoreSlug(ref slug, item.Id);
                item.Slug = slug;
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Post, item.Id, item.Title, "Khôi phục từ thùng rác");
                break;
            }
            case AuditEntities.Page:
            {
                var item = await db.CustomPages.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return NotFound();
                SoftDelete.Restore(item);
                var slug = item.Slug;
                SoftDelete.RestoreSlug(ref slug, item.Id);
                item.Slug = slug;
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Page, item.Id, item.Title, "Khôi phục từ thùng rác");
                break;
            }
            case AuditEntities.Media:
            {
                var ok = await media.RestoreAsync(id);
                if (!ok) return NotFound();
                await audit.LogAsync(AuditActions.Restore, AuditEntities.Media, id, null, "Khôi phục từ thùng rác");
                break;
            }
            default:
                return BadRequest();
        }

        TempData["Message"] = "Đã khôi phục.";
        return RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            EntityType = entityType,
            Q = q
        });
    }

    public async Task<IActionResult> OnPostHardDeleteAsync(
        string type, int id, int pageNumber = 1, int pageSize = AdminPaging.DefaultPageSize,
        string? entityType = null, string? q = null)
    {
        switch (type)
        {
            case AuditEntities.Product:
            {
                var item = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return NotFound();
                var title = item.Name;
                db.Products.Remove(item);
                var error = await DbSave.TryDeleteAsync(db);
                if (error is not null)
                {
                    TempData["Error"] = error;
                    return RedirectToPage(new
                    {
                        PageNumber = pageNumber,
                        PageSize = AdminPaging.NormalizeSize(pageSize),
                        EntityType = entityType,
                        Q = q
                    });
                }

                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Product, id, title, "Xóa vĩnh viễn");
                break;
            }
            case AuditEntities.Post:
            {
                var item = await db.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return NotFound();
                var title = item.Title;
                db.Posts.Remove(item);
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Post, id, title, "Xóa vĩnh viễn");
                break;
            }
            case AuditEntities.Page:
            {
                var item = await db.CustomPages.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted);
                if (item is null) return NotFound();
                var title = item.Title;
                db.CustomPages.Remove(item);
                await db.SaveChangesAsync();
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Page, id, title, "Xóa vĩnh viễn");
                break;
            }
            case AuditEntities.Media:
            {
                var ok = await media.HardDeleteAsync(id);
                if (!ok) return NotFound();
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Media, id, null, "Xóa vĩnh viễn");
                break;
            }
            default:
                return BadRequest();
        }

        TempData["Message"] = "Đã xóa vĩnh viễn.";
        return RedirectToPage(new
        {
            PageNumber = pageNumber,
            PageSize = AdminPaging.NormalizeSize(pageSize),
            EntityType = entityType,
            Q = q
        });
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/Trash", page, size, ("EntityType", EntityType), ("Q", Q));

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
