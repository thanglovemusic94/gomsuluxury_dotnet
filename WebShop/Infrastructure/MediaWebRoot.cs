namespace WebShop.Infrastructure;

/// <summary>WebRoot path for media File.Exists checks (set once at startup).</summary>
public static class MediaWebRoot
{
    public static string? Path { get; private set; }

    public static void Configure(string webRootPath) =>
        Path = string.IsNullOrWhiteSpace(webRootPath) ? null : webRootPath;

    public static bool FileExistsForUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(Path))
            return false;

        var relative = url;
        var q = relative.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
            relative = relative[..q];

        relative = relative.TrimStart('~').TrimStart('/');
        if (relative.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return false;

        var abs = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        return File.Exists(abs);
    }
}
