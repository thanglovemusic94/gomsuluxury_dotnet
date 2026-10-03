namespace WebShop.Infrastructure;

/// <summary>
/// Clears ImageSharp.Web disk cache (hashed filenames — safest to wipe folder).
/// </summary>
public static class MediaImageCache
{
    public static void PurgeAll(IWebHostEnvironment env)
    {
        var root = Path.Combine(env.WebRootPath, MediaImageSharpSetup.CacheFolderName);
        if (!Directory.Exists(root))
            return;

        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Best-effort: ignore locked files
        }

        Directory.CreateDirectory(root);
    }
}
