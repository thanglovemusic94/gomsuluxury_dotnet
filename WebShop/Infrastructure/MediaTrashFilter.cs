using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebShop.Data;

namespace WebShop.Infrastructure;

/// <summary>
/// Chặn URL media đã soft-delete (Thùng rác) khi render nội dung / chọn ảnh.
/// File trên đĩa có thể còn; không dùng lại trong content cho đến khi khôi phục.
/// </summary>
public sealed class MediaTrashFilter(AppDbContext db, IMemoryCache cache)
{
    public const string CacheKey = "media:trash-url-index-v1";

    private static readonly Regex ImgTag = new(
        @"<img\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SrcAttr = new(
        @"\bsrc\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void Invalidate() => cache.Remove(CacheKey);

    public async Task<string?> LiveOrNullAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var index = await GetIndexAsync(ct);
        return index.IsTrashed(url) ? null : url.Trim();
    }

    public async Task<string> ScrubHtmlAsync(string? html, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(html) || html.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase) < 0)
            return html ?? string.Empty;

        var index = await GetIndexAsync(ct);
        if (!index.HasTrash)
            return html;

        return ImgTag.Replace(html, match =>
        {
            var srcMatch = SrcAttr.Match(match.Value);
            if (!srcMatch.Success)
                return match.Value;
            var src = srcMatch.Groups[1].Success ? srcMatch.Groups[1].Value
                : srcMatch.Groups[2].Success ? srcMatch.Groups[2].Value
                : srcMatch.Groups[3].Value;
            return index.IsTrashed(src) ? string.Empty : match.Value;
        });
    }

    private async Task<TrashIndex> GetIndexAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out TrashIndex? cached) && cached is not null)
            return cached;

        var rows = await db.MediaAssets.AsNoTracking()
            .IgnoreQueryFilters()
            .Select(item => new
            {
                item.IsDeleted,
                item.OriginalUrl,
                item.ThumbUrl,
                item.MediumUrl,
                item.LargeUrl
            })
            .ToListAsync(ct);

        var livePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var liveFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deadFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var paths = row.IsDeleted ? deadPaths : livePaths;
            var files = row.IsDeleted ? deadFiles : liveFiles;
            AddUrl(paths, files, row.OriginalUrl);
            AddUrl(paths, files, row.ThumbUrl);
            AddUrl(paths, files, row.MediumUrl);
            AddUrl(paths, files, row.LargeUrl);
        }

        var index = new TrashIndex(livePaths, deadPaths, liveFiles, deadFiles);
        cache.Set(CacheKey, index, TimeSpan.FromMinutes(5));
        return index;
    }

    private static void AddUrl(HashSet<string> paths, HashSet<string> files, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        void AddOne(string? value)
        {
            var path = NormalizePath(value);
            if (string.IsNullOrEmpty(path))
                return;
            paths.Add(path);
            var file = FileName(path);
            if (!string.IsNullOrEmpty(file))
                files.Add(file);
        }

        AddOne(url);
        AddOne(MediaUrls.For(url, MediaSize.Icon));
        AddOne(MediaUrls.For(url, MediaSize.Thumb));
        AddOne(MediaUrls.For(url, MediaSize.Medium));
        AddOne(MediaUrls.For(url, MediaSize.Large));
        AddOne(MediaUrls.For(url, MediaSize.Original));
    }

    internal static string NormalizePath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var value = url.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
            value = "https:" + value;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            value = absolute.AbsolutePath;
        else
        {
            var q = value.IndexOf('?', StringComparison.Ordinal);
            if (q >= 0)
                value = value[..q];
            var h = value.IndexOf('#', StringComparison.Ordinal);
            if (h >= 0)
                value = value[..h];
        }

        if (!value.StartsWith('/'))
            value = "/" + value;
        return value;
    }

    private static string FileName(string path)
    {
        var i = path.LastIndexOf('/');
        return i >= 0 ? path[(i + 1)..] : path;
    }

    private sealed class TrashIndex(
        HashSet<string> livePaths,
        HashSet<string> deadPaths,
        HashSet<string> liveFiles,
        HashSet<string> deadFiles)
    {
        public bool HasTrash => deadPaths.Count > 0 || deadFiles.Count > 0;

        public bool IsTrashed(string? url)
        {
            var path = NormalizePath(url);
            if (string.IsNullOrEmpty(path))
                return false;

            var file = FileName(path);
            if (livePaths.Contains(path) || (!string.IsNullOrEmpty(file) && liveFiles.Contains(file)))
                return false;

            if (deadPaths.Contains(path))
                return true;

            return !string.IsNullOrEmpty(file) && deadFiles.Contains(file);
        }
    }
}
