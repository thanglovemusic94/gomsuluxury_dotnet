using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebShop.Infrastructure;

namespace WebShop.Pages.Admin.Media;

[Authorize]
[IgnoreAntiforgeryToken]
public class UploadModel(MediaStorage media) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync(string? type, string? folder, string? responseType, string? CKEditorFuncNum, CancellationToken ct)
    {
        type = string.IsNullOrWhiteSpace(type) ? "Images" : type;
        var targetFolder = MediaFolders.Normalize(folder);
        if (targetFolder == MediaFolders.All)
            targetFolder = MediaFolders.Pages;

        var file = Request.Form.Files.FirstOrDefault(f => f.Length > 0)
            ?? Request.Form.Files.FirstOrDefault();

        if (file is null)
            return UploadFail(responseType, CKEditorFuncNum, "Không có file.");

        var (ok, url, error, asset) = await media.SaveAsync(file, type, null, targetFolder, ct);
        if (!ok || string.IsNullOrWhiteSpace(url))
            return UploadFail(responseType, CKEditorFuncNum, error ?? "Upload thất bại.");

        // CKEditor / content inserts use medium optimized URL.
        url = asset?.MediumUrl ?? url;

        if (string.Equals(responseType, "json", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new
            {
                uploaded = 1,
                fileName = Path.GetFileName(url),
                url
            });
        }

        var funcNum = CKEditorFuncNum ?? Request.Query["CKEditorFuncNum"].ToString();
        var message = string.Empty;
        var script = $"window.parent.CKEDITOR.tools.callFunction({funcNum}, {System.Text.Json.JsonSerializer.Serialize(url)}, {System.Text.Json.JsonSerializer.Serialize(message)});";
        return Content($"<script type=\"text/javascript\">{script}</script>", "text/html");
    }

    private IActionResult UploadFail(string? responseType, string? CKEditorFuncNum, string message)
    {
        if (string.Equals(responseType, "json", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new
            {
                uploaded = 0,
                error = new { message }
            });
        }

        var funcNum = CKEditorFuncNum ?? Request.Query["CKEditorFuncNum"].ToString();
        if (string.IsNullOrWhiteSpace(funcNum))
            return BadRequest(message);

        var script = $"window.parent.CKEDITOR.tools.callFunction({funcNum}, '', {System.Text.Json.JsonSerializer.Serialize(message)});";
        return Content($"<script type=\"text/javascript\">{script}</script>", "text/html");
    }
}
