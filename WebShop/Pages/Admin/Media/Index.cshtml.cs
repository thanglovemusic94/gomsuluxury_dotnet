using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using WebShop.Infrastructure;

namespace WebShop.Pages.Admin.Media;

public class IndexModel(MediaStorage media, MediaUsageService usage, AuditService audit, MediaFolderService folders, IOptions<MediaOptions> mediaOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = "Images";

    [BindProperty(SupportsGet = true)]
    public string Folder { get; set; } = MediaFolders.All;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = AdminPaging.DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool MissingAlt { get; set; }

    public MediaPageResult PageData { get; private set; } = new();

    public IReadOnlyList<MediaFolderEntry> FolderList { get; private set; } = [];

    public IReadOnlyList<MediaFolderEntry> ChildFolders { get; private set; } = [];

    public int DeleteFolderFileCount { get; private set; }

    public int DeleteFolderSubfolderCount { get; private set; }

    public Dictionary<int, int> UsageCounts { get; private set; } = new();

    public long MaxUploadBytes => mediaOptions.Value.MaxUploadBytes;

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageSize = AdminPaging.NormalizeSize(PageSize);
        FolderList = await folders.ListAsync(ct);
        ChildFolders = MediaFolderService.ImmediateChildren(FolderList, Folder);
        PageData = await media.ListPagedAsync(new MediaQuery
        {
            Type = Type,
            Folder = Folder,
            Q = Q,
            MissingAlt = MissingAlt,
            Page = PageNumber,
            PageSize = PageSize
        }, ct);
        PageNumber = PageData.Page;
        PageSize = PageData.PageSize;

        var impact = await folders.GetDeleteImpactAsync(Folder, ct);
        DeleteFolderFileCount = impact.Files;
        DeleteFolderSubfolderCount = impact.Subfolders;

        var ids = PageData.Items.Where(item => item.Id > 0).Select(item => item.Id).ToList();
        UsageCounts = await usage.CountUsagesAsync(ids, ct);
    }

    private object KeepList(int? pageNumber = null, string? folder = null) => new
    {
        Type,
        Folder = folder ?? Folder,
        PageNumber = pageNumber ?? PageNumber,
        PageSize,
        Q,
        MissingAlt = MissingAlt ? "true" : null
    };

    public async Task<IActionResult> OnGetUsageAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
            return new JsonResult(Array.Empty<MediaUsageRef>());

        var items = await usage.GetUsagesAsync(id, ct);
        return new JsonResult(items, SeoJsonOptions);
    }

    public async Task<IActionResult> OnPostCreateFolderAsync(string? newFolderName, CancellationToken ct)
    {
        var parent = MediaFolders.Normalize(Folder);
        var (ok, key, error) = await folders.CreateAsync(newFolderName, parent, ct);
        TempData[ok ? "Message" : "Error"] = ok
            ? $"Đã tạo thư mục «{MediaFolders.Label(key)}»."
            : error ?? "Tạo thư mục thất bại.";
        return RedirectToPage(KeepList(1, ok && !string.IsNullOrWhiteSpace(key) ? key : parent));
    }

    public async Task<IActionResult> OnPostDeleteFolderAsync(CancellationToken ct)
    {
        var target = MediaFolders.Normalize(Folder);
        var (ok, parent, files, subfolders, error) = await folders.DeleteAsync(
            target, User.Identity?.Name, ct);
        if (ok)
        {
            TempData["Message"] =
                $"Đã xóa «{MediaFolders.Label(target)}»" +
                (subfolders > 0 ? $", {subfolders} thư mục con" : "") +
                (files > 0 ? $", {files} file vào thùng rác" : "") +
                ".";
            await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Media, 0, null,
                $"Xóa thư mục media «{MediaFolders.Label(target)}»",
                $"files={files}; subfolders={subfolders}", ct);
            return RedirectToPage(KeepList(1, parent ?? MediaFolders.All));
        }

        TempData["Error"] = error ?? "Xóa thư mục thất bại.";
        return RedirectToPage(KeepList());
    }

    public async Task<IActionResult> OnPostRenameFolderAsync(string? newFolderName, CancellationToken ct)
    {
        var target = MediaFolders.Normalize(Folder);
        var (ok, newKey, error) = await folders.RenameAsync(target, newFolderName, ct);
        TempData[ok ? "Message" : "Error"] = ok
            ? $"Đã đổi tên thư mục → «{MediaFolders.Label(newKey)}»."
            : error ?? "Đổi tên thư mục thất bại.";
        return RedirectToPage(KeepList(1, ok && !string.IsNullOrWhiteSpace(newKey) ? newKey : target));
    }

    public async Task<IActionResult> OnPostMoveFolderAsync(string? targetParent, CancellationToken ct)
    {
        var source = MediaFolders.Normalize(Folder);
        var (ok, newKey, error) = await folders.MoveAsync(source, targetParent, ct);
        TempData[ok ? "Message" : "Error"] = ok
            ? $"Đã chuyển thư mục → «{MediaFolders.Label(newKey)}»."
            : error ?? "Di chuyển thư mục thất bại.";
        return RedirectToPage(KeepList(1, ok && !string.IsNullOrWhiteSpace(newKey) ? newKey : source));
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, string? alt, string? folder, CancellationToken ct)
    {
        var targetFolder = MediaFolders.Normalize(folder);
        if (targetFolder == MediaFolders.All)
            targetFolder = MediaFolders.Other;

        if (file is null || file.Length == 0)
            TempData["Error"] = "Chọn file để tải lên.";
        else
        {
            var (ok, url, error, _) = await media.SaveAsync(file, Type, alt, targetFolder, ct);
            TempData[ok ? "Message" : "Error"] = ok
                ? $"Đã tải lên ({MediaFolders.Label(targetFolder)}): {url}"
                : error;
        }

        return RedirectToPage(KeepList(1, targetFolder));
    }

    public async Task<IActionResult> OnPostUploadAjaxAsync(IFormFile? file, string? alt, string? folder, CancellationToken ct)
    {
        var targetFolder = MediaFolders.Normalize(folder);
        if (targetFolder == MediaFolders.All)
            targetFolder = MediaFolders.Other;

        if (file is null || file.Length == 0)
            return new JsonResult(new { ok = false, error = "File trống." }, SeoJsonOptions);

        var (ok, url, error, asset) = await media.SaveAsync(file, Type, alt, targetFolder, ct);
        return new JsonResult(new
        {
            ok,
            url,
            error,
            id = asset?.Id ?? 0,
            name = asset?.FileName,
            folder = targetFolder
        }, SeoJsonOptions);
    }

    public async Task<IActionResult> OnPostAltAsync(int id, string? alt, CancellationToken ct)
    {
        var (ok, error) = await media.UpdateAltAsync(id, alt, ct);
        TempData[ok ? "Message" : "Error"] = ok ? "Đã lưu alt." : error;
        return RedirectToPage(KeepList());
    }

    public async Task<IActionResult> OnPostFolderAsync(int id, string? folder, CancellationToken ct)
    {
        var (ok, error) = await media.UpdateFolderAsync(id, folder, ct);
        TempData[ok ? "Message" : "Error"] = ok ? "Đã đổi thư mục." : error;
        return RedirectToPage(KeepList());
    }

    public async Task<IActionResult> OnPostRenameAsync(int id, string? newName, CancellationToken ct)
    {
        var (ok, error) = await media.RenameAsync(id, newName, ct);
        TempData[ok ? "Message" : "Error"] = ok ? "Đã đổi tên file." : error ?? "Đổi tên thất bại.";
        return RedirectToPage(KeepList());
    }

    public async Task<IActionResult> OnPostCopyAsync(int id, string? folder, CancellationToken ct)
    {
        var dest = MediaFolders.Normalize(folder);
        if (dest == MediaFolders.All)
            dest = MediaFolders.Other;
        var (ok, error) = await media.CopyToFolderAsync(id, dest, ct);
        TempData[ok ? "Message" : "Error"] = ok
            ? $"Đã sao chép sang «{MediaFolders.Label(dest)}»."
            : error ?? "Sao chép thất bại.";
        return RedirectToPage(KeepList(1, ok ? dest : null));
    }

    public async Task<IActionResult> OnPostBulkCopyAsync(int[]? ids, string? folder, CancellationToken ct)
    {
        ids ??= [];
        if (ids.Length == 0)
        {
            TempData["Error"] = "Chưa chọn ảnh.";
            return RedirectToPage(KeepList());
        }

        var dest = MediaFolders.Normalize(folder);
        if (dest == MediaFolders.All)
            dest = MediaFolders.Other;

        var okCount = 0;
        string? lastError = null;
        foreach (var id in ids.Distinct())
        {
            var (ok, error) = await media.CopyToFolderAsync(id, dest, ct);
            if (ok) okCount++;
            else lastError = error;
        }

        TempData[okCount > 0 ? "Message" : "Error"] = okCount > 0
            ? $"Đã sao chép {okCount}/{ids.Length} file sang «{MediaFolders.Label(dest)}»."
            : lastError ?? "Sao chép thất bại.";
        return RedirectToPage(KeepList(1, dest));
    }

    public async Task<IActionResult> OnPostBulkFolderAsync(int[]? ids, string? folder, CancellationToken ct)
    {
        ids ??= [];
        if (ids.Length == 0)
        {
            TempData["Error"] = "Chưa chọn ảnh.";
            return RedirectToPage(KeepList());
        }

        var okCount = 0;
        foreach (var id in ids.Distinct())
        {
            var (ok, _) = await media.UpdateFolderAsync(id, folder, ct);
            if (ok) okCount++;
        }

        TempData["Message"] = $"Đã chuyển {okCount}/{ids.Length} ảnh sang «{MediaFolders.Label(folder)}».";
        return RedirectToPage(KeepList(1, folder));
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync(int[]? ids, CancellationToken ct)
    {
        ids ??= [];
        if (ids.Length == 0)
        {
            TempData["Error"] = "Chưa chọn ảnh.";
            return RedirectToPage(KeepList());
        }

        var okCount = 0;
        foreach (var id in ids.Distinct())
        {
            var usages = await usage.GetUsagesAsync(id, ct);
            var ok = await media.DeleteAsync(id, User.Identity?.Name, ct);
            if (!ok) continue;
            okCount++;
            await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Media, id, null,
                "Đưa vào thùng rác (bulk)",
                usages.Count > 0 ? $"Còn {usages.Count} tham chiếu" : null, ct);
        }

        TempData["Message"] = $"Đã chuyển {okCount}/{ids.Length} ảnh vào thùng rác.";
        return RedirectToPage(KeepList());
    }

    public async Task<IActionResult> OnPostRegenerateAsync(int id, CancellationToken ct)
    {
        var (ok, error) = await media.RegenerateAsync(id, ct);
        TempData[ok ? "Message" : "Error"] = ok ? "Đã tạo lại bản tối ưu." : error;
        return RedirectToPage(KeepList());
    }

    public async Task<IActionResult> OnPostRegenerateAllAsync(CancellationToken ct)
    {
        var (_, error) = await media.RegenerateAllAsync(ct);
        TempData["Message"] = error ?? "Đã tạo lại tất cả bản tối ưu.";
        return RedirectToPage(KeepList());
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? fileName, CancellationToken ct)
    {
        if (id > 0)
        {
            var usages = await usage.GetUsagesAsync(id, ct);
            var ok = await media.DeleteAsync(id, User.Identity?.Name, ct);
            if (ok)
            {
                var detail = usages.Count > 0
                    ? $"FileName: {fileName}; vẫn còn {usages.Count} tham chiếu nội dung"
                    : $"FileName: {fileName}";
                await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Media, id, fileName, "Đưa vào thùng rác",
                    detail, ct);
            }

            TempData[ok ? "Message" : "Error"] = ok ? "Đã chuyển media vào thùng rác." : "Không xóa được file.";
        }
        else
        {
            var ok = media.Delete(fileName ?? string.Empty);
            TempData[ok ? "Message" : "Error"] = ok ? "Đã xóa file cũ." : "Không xóa được file.";
        }

        return RedirectToPage(KeepList());
    }

    private static readonly JsonSerializerOptions SeoJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<IActionResult> OnPostPreviewSeoAsync([FromForm] int[]? ids, CancellationToken ct)
    {
        var items = await media.PreviewSeoOptimizeAsync(ids ?? [], ct);
        return new JsonResult(items, SeoJsonOptions);
    }

    public async Task<IActionResult> OnPostApplySeoAsync([FromForm] string? payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new JsonResult(new MediaSeoApplyResult(0, 0, 0, 0, 0, "Không có ảnh được chọn."), SeoJsonOptions);

        List<MediaSeoApplyItem>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<MediaSeoApplyItem>>(payload, SeoJsonOptions);
        }
        catch (JsonException)
        {
            return new JsonResult(new MediaSeoApplyResult(0, 0, 0, 0, 1, "Dữ liệu không hợp lệ."), SeoJsonOptions);
        }

        var result = await media.ApplySeoOptimizeAsync(items ?? [], ct);
        return new JsonResult(result, SeoJsonOptions);
    }
}
