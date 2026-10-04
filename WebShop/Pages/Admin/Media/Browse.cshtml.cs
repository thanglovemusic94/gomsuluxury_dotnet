using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebShop.Infrastructure;

namespace WebShop.Pages.Admin.Media;

[Authorize]
public class BrowseModel(MediaStorage media, MediaUsageService usage, AuditService audit, MediaFolderService folders) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = "Images";

    [BindProperty(SupportsGet = true)]
    public string Size { get; set; } = "medium";

    [BindProperty(SupportsGet = true)]
    public string Folder { get; set; } = MediaFolders.All;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? CKEditor { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CKEditorFuncNum { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? InputId { get; set; }

    public MediaPageResult PageData { get; private set; } = new();

    public IReadOnlyList<MediaFolderEntry> FolderList { get; private set; } = [];

    /// <summary>Thư mục con trực tiếp của folder đang mở (hiện trong lưới nội dung).</summary>
    public IReadOnlyList<MediaFolderEntry> ChildFolders { get; private set; } = [];

    public string? Flash { get; private set; }

    public int DeleteFolderFileCount { get; private set; }

    public int DeleteFolderSubfolderCount { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (TempData.TryGetValue("BrowseFlash", out var flash) && flash is string s)
            Flash = s;
        FolderList = await folders.ListAsync(ct);
        ChildFolders = MediaFolderService.ImmediateChildren(FolderList, Folder);
        PageData = await LoadAsync(ct);
        var impact = await folders.GetDeleteImpactAsync(Folder, ct);
        DeleteFolderFileCount = impact.Files;
        DeleteFolderSubfolderCount = impact.Subfolders;
    }

    public async Task<IActionResult> OnPostUploadAsync(List<IFormFile>? files, IFormFile? file, string? folder, CancellationToken ct)
    {
        var target = MediaFolders.Normalize(folder);
        if (target == MediaFolders.All)
            target = MediaFolders.Other;

        var batch = new List<IFormFile>();
        if (files is { Count: > 0 })
            batch.AddRange(files.Where(f => f is { Length: > 0 }));
        if (file is { Length: > 0 })
            batch.Add(file);

        if (batch.Count == 0)
            Flash = "Chọn file để tải lên.";
        else
        {
            var okCount = 0;
            string? lastError = null;
            foreach (var item in batch)
            {
                var (ok, _, error, _) = await media.SaveAsync(item, Type, null, target, ct);
                if (ok) okCount++;
                else lastError = error;
            }

            Flash = okCount > 0
                ? $"Đã tải lên {okCount}/{batch.Count} file ({MediaFolders.Label(target)})."
                : lastError ?? "Upload thất bại.";
        }

        Folder = target;
        PageNumber = 1;
        FolderList = await folders.ListAsync(ct);
        ChildFolders = MediaFolderService.ImmediateChildren(FolderList, Folder);
        PageData = await LoadAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateFolderAsync(string? newFolderName, CancellationToken ct)
    {
        var parent = MediaFolders.Normalize(Folder);
        var (ok, key, error) = await folders.CreateAsync(newFolderName, parent, ct);
        TempData["BrowseFlash"] = ok
            ? $"Đã tạo thư mục «{MediaFolders.Label(key)}»."
            : error ?? "Tạo thư mục thất bại.";
        if (ok && !string.IsNullOrWhiteSpace(key))
        {
            Folder = key!;
            PageNumber = 1;
        }

        return RedirectToBrowse();
    }

    public async Task<IActionResult> OnPostDeleteFolderAsync(CancellationToken ct)
    {
        var target = MediaFolders.Normalize(Folder);
        var (ok, parent, files, subfolders, error) = await folders.DeleteAsync(
            target, User.Identity?.Name, ct);
        if (ok)
        {
            TempData["BrowseFlash"] =
                $"Đã xóa «{MediaFolders.Label(target)}»" +
                (subfolders > 0 ? $", {subfolders} thư mục con" : "") +
                (files > 0 ? $", {files} file vào thùng rác" : "") +
                ".";
            await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Media, 0, null,
                $"Xóa thư mục media «{MediaFolders.Label(target)}»",
                $"files={files}; subfolders={subfolders}", ct);
            Folder = parent ?? MediaFolders.All;
            PageNumber = 1;
        }
        else
        {
            TempData["BrowseFlash"] = error ?? "Xóa thư mục thất bại.";
        }

        return RedirectToBrowse();
    }

    public async Task<IActionResult> OnPostRenameFolderAsync(string? newFolderName, CancellationToken ct)
    {
        var target = MediaFolders.Normalize(Folder);
        var (ok, newKey, error) = await folders.RenameAsync(target, newFolderName, ct);
        TempData["BrowseFlash"] = ok
            ? $"Đã đổi tên thư mục → «{MediaFolders.Label(newKey)}»."
            : error ?? "Đổi tên thư mục thất bại.";
        if (ok && !string.IsNullOrWhiteSpace(newKey))
        {
            Folder = newKey!;
            PageNumber = 1;
        }

        return RedirectToBrowse();
    }

    public async Task<IActionResult> OnPostMoveFolderAsync(string? targetParent, CancellationToken ct)
    {
        var source = MediaFolders.Normalize(Folder);
        var (ok, newKey, error) = await folders.MoveAsync(source, targetParent, ct);
        TempData["BrowseFlash"] = ok
            ? $"Đã chuyển thư mục → «{MediaFolders.Label(newKey)}»."
            : error ?? "Di chuyển thư mục thất bại.";
        if (ok && !string.IsNullOrWhiteSpace(newKey))
        {
            Folder = newKey!;
            PageNumber = 1;
        }

        return RedirectToBrowse();
    }

    public async Task<IActionResult> OnPostBulkFolderAsync(int[]? ids, string? targetFolder, CancellationToken ct)
    {
        ids ??= [];
        if (ids.Length == 0)
        {
            TempData["BrowseFlash"] = "Chưa chọn file.";
            return RedirectToBrowse();
        }

        var dest = MediaFolders.Normalize(targetFolder);
        if (dest == MediaFolders.All)
            dest = MediaFolders.Other;

        var okCount = 0;
        foreach (var id in ids.Distinct())
        {
            var (ok, _) = await media.UpdateFolderAsync(id, dest, ct);
            if (ok) okCount++;
        }

        TempData["BrowseFlash"] = $"Đã chuyển {okCount}/{ids.Length} file sang «{MediaFolders.Label(dest)}».";
        Folder = dest;
        PageNumber = 1;
        return RedirectToBrowse();
    }

    public async Task<IActionResult> OnPostBulkCopyAsync(int[]? ids, string? targetFolder, CancellationToken ct)
    {
        ids ??= [];
        if (ids.Length == 0)
        {
            TempData["BrowseFlash"] = "Chưa chọn file.";
            return RedirectToBrowse();
        }

        var dest = MediaFolders.Normalize(targetFolder);
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

        TempData["BrowseFlash"] = okCount > 0
            ? $"Đã sao chép {okCount}/{ids.Length} file sang «{MediaFolders.Label(dest)}»."
            : lastError ?? "Sao chép thất bại.";
        Folder = dest;
        PageNumber = 1;
        return RedirectToBrowse();
    }

    public async Task<IActionResult> OnPostRenameAsync(int id, string? newName, CancellationToken ct)
    {
        var (ok, error) = await media.RenameAsync(id, newName, ct);
        TempData["BrowseFlash"] = ok ? "Đã đổi tên file." : error ?? "Đổi tên thất bại.";
        return RedirectToBrowse();
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync(int[]? ids, CancellationToken ct)
    {
        ids ??= [];
        if (ids.Length == 0)
        {
            TempData["BrowseFlash"] = "Chưa chọn file.";
            return RedirectToBrowse();
        }

        var okCount = 0;
        foreach (var id in ids.Distinct())
        {
            var usages = await usage.GetUsagesAsync(id, ct);
            var ok = await media.DeleteAsync(id, User.Identity?.Name, ct);
            if (!ok) continue;
            okCount++;
            await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Media, id, null,
                "Đưa vào thùng rác (picker)",
                usages.Count > 0 ? $"Còn {usages.Count} tham chiếu" : null, ct);
        }

        TempData["BrowseFlash"] = $"Đã chuyển {okCount}/{ids.Length} file vào thùng rác.";
        return RedirectToBrowse();
    }

    private async Task<MediaPageResult> LoadAsync(CancellationToken ct) =>
        await media.ListPagedAsync(new MediaQuery
        {
            Type = Type,
            Folder = Folder,
            Page = PageNumber,
            PageSize = MediaFolders.PageSize
        }, ct);

    private RedirectToPageResult RedirectToBrowse() =>
        RedirectToPage(new
        {
            Type,
            Size,
            Folder,
            PageNumber,
            CKEditor,
            CKEditorFuncNum,
            InputId
        });
}
