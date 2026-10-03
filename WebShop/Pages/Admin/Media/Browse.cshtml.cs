using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebShop.Infrastructure;

namespace WebShop.Pages.Admin.Media;

[Authorize]
public class BrowseModel(MediaStorage media) : PageModel
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

    public string? Flash { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageData = await media.ListPagedAsync(new MediaQuery
        {
            Type = Type,
            Folder = Folder,
            Page = PageNumber,
            PageSize = MediaFolders.PageSize
        }, ct);
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, string? folder, CancellationToken ct)
    {
        var target = MediaFolders.Normalize(folder);
        if (target == MediaFolders.All)
            target = MediaFolders.Other;

        if (file is null || file.Length == 0)
            Flash = "Chọn file để tải lên.";
        else
        {
            var (ok, _, error, _) = await media.SaveAsync(file, Type, null, target, ct);
            Flash = ok ? $"Đã tải lên ({MediaFolders.Label(target)})." : error;
        }

        Folder = target;
        PageNumber = 1;
        PageData = await media.ListPagedAsync(new MediaQuery
        {
            Type = Type,
            Folder = Folder,
            Page = PageNumber,
            PageSize = MediaFolders.PageSize
        }, ct);
        return Page();
    }
}
