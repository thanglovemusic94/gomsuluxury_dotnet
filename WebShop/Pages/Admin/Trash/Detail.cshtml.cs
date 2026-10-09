using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Trash;

public class DetailModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public string Title { get; private set; } = string.Empty;

    public DateTime? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public IList<(string Label, string? Value)> Fields { get; private set; } = [];

    public string? PreviewUrl { get; private set; }

    /// <summary>Warning when hard-delete will also clear order lines / reviews (Trash still allows proceed).</summary>
    public string? HardDeleteWarning { get; private set; }

    public IList<AuditLog> RelatedLogs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        switch (Type)
        {
            case AuditEntities.Product:
            {
                var item = await db.Products.IgnoreQueryFilters().AsNoTracking()
                    .Include(row => row.Category)
                    .FirstOrDefaultAsync(row => row.Id == Id && row.IsDeleted);
                if (item is null) return NotFound();
                Title = item.Name;
                DeletedAt = item.DeletedAt;
                DeletedBy = item.DeletedBy;
                PreviewUrl = item.ImageUrl;
                var blockers = await ProductDeleteGuard.GetBlockersAsync(db, item.Id);
                HardDeleteWarning = ProductDeleteGuard.TrashWarning(blockers);
                Fields =
                [
                    ("ID", item.Id.ToString()),
                    ("Tên", item.Name),
                    ("Slug", item.Slug),
                    ("Danh mục", item.Category?.Name),
                    ("Giá", item.Price.ToString("N0")),
                    ("Giá KM", item.DiscountPrice?.ToString("N0")),
                    ("Giá vốn", item.CostPrice.ToString("N0")),
                    ("Tồn kho", item.Stock.ToString()),
                    ("Hiển thị", item.IsVisible ? "Có" : "Không"),
                    ("Xóa cứng", blockers.Summary),
                    ("Ảnh", item.ImageUrl),
                    ("Mô tả ngắn", item.ShortDescription),
                    ("Mô tả", Truncate(item.Description, 800)),
                    ("Meta title", item.MetaTitle),
                    ("Meta description", item.MetaDescription)
                ];
                break;
            }
            case AuditEntities.Post:
            {
                var item = await db.Posts.IgnoreQueryFilters().AsNoTracking()
                    .Include(row => row.Category)
                    .Include(row => row.Author)
                    .FirstOrDefaultAsync(row => row.Id == Id && row.IsDeleted);
                if (item is null) return NotFound();
                Title = item.Title;
                DeletedAt = item.DeletedAt;
                DeletedBy = item.DeletedBy;
                PreviewUrl = item.ImageUrl;
                Fields =
                [
                    ("ID", item.Id.ToString()),
                    ("Tiêu đề", item.Title),
                    ("Slug", item.Slug),
                    ("Danh mục", item.Category?.Name),
                    ("Tác giả", item.Author?.Username),
                    ("Xuất bản", item.IsPublished ? "Có" : "Không"),
                    ("Ảnh", item.ImageUrl),
                    ("Tóm tắt", item.Summary),
                    ("Nội dung", Truncate(item.Content, 800)),
                    ("Tạo lúc", item.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
                    ("Cập nhật", item.UpdatedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))
                ];
                break;
            }
            case AuditEntities.Page:
            {
                var item = await db.CustomPages.IgnoreQueryFilters().AsNoTracking()
                    .FirstOrDefaultAsync(row => row.Id == Id && row.IsDeleted);
                if (item is null) return NotFound();
                Title = item.Title;
                DeletedAt = item.DeletedAt;
                DeletedBy = item.DeletedBy;
                Fields =
                [
                    ("ID", item.Id.ToString()),
                    ("Tiêu đề", item.Title),
                    ("Slug", item.Slug),
                    ("Xuất bản", item.IsPublished ? "Có" : "Không"),
                    ("Nội dung", Truncate(item.Content, 800)),
                    ("Meta title", item.MetaTitle),
                    ("Meta description", item.MetaDescription),
                    ("Tạo lúc", item.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))
                ];
                break;
            }
            case AuditEntities.Media:
            {
                var item = await db.MediaAssets.IgnoreQueryFilters().AsNoTracking()
                    .FirstOrDefaultAsync(row => row.Id == Id && row.IsDeleted);
                if (item is null) return NotFound();
                Title = item.FileName;
                DeletedAt = item.DeletedAt;
                DeletedBy = item.DeletedBy;
                PreviewUrl = item.ThumbUrl ?? item.MediumUrl ?? item.OriginalUrl;
                Fields =
                [
                    ("ID", item.Id.ToString()),
                    ("Tên file", item.FileName),
                    ("Alt", item.Alt),
                    ("Thư mục", MediaFolders.Label(item.Folder)),
                    ("Ảnh?", item.IsImage ? "Có" : "Không"),
                    ("URL gốc", item.OriginalUrl),
                    ("Thumb", item.ThumbUrl),
                    ("Medium", item.MediumUrl),
                    ("Large", item.LargeUrl),
                    ("Dung lượng", $"{item.OriginalBytes / 1024.0:0.#} KB"),
                    ("Tạo lúc", item.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))
                ];
                break;
            }
            default:
                return NotFound();
        }

        RelatedLogs = await db.AuditLogs.AsNoTracking()
            .Where(item => item.EntityType == Type && item.EntityId == Id)
            .OrderByDescending(item => item.CreatedAt)
            .Take(20)
            .ToListAsync();

        return Page();
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        value = value.Trim();
        return value.Length <= max ? value : value[..max] + "...";
    }
}
