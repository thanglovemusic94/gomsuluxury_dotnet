using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public sealed class MediaOptions
{
    public int IconWidth { get; set; } = 96;
    public int ThumbWidth { get; set; } = 400;
    public int MediumWidth { get; set; } = 800;
    public int LargeWidth { get; set; } = 1200;
    public int WebpQuality { get; set; } = 78;
    public long MaxUploadBytes { get; set; } = 15 * 1024 * 1024;
    public bool DedupeOnUpload { get; set; } = true;
    public bool GarbageCollectEnabled { get; set; } = true;
    public int GarbageCollectIntervalDays { get; set; } = 7;
}

public sealed partial class MediaStorage(IWebHostEnvironment env, IServiceScopeFactory scopes, IOptions<MediaOptions> options)
{
    private static readonly HashSet<string> ImageExt =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    private static readonly HashSet<string> PassThroughImageExt =
        new(StringComparer.OrdinalIgnoreCase) { ".svg", ".gif" };

    private static readonly HashSet<string> FileExt =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".zip", ".rar", ".txt", ".mp4", ".webm", ".mp3"
        };

    private readonly MediaOptions _options = options.Value;
    private bool _schemaReady;

    public string RootPath => Path.Combine(env.WebRootPath, "uploads");

    public void EnsureRoot()
    {
        Directory.CreateDirectory(Path.Combine(RootPath, "originals"));
        Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "icon"));
        Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "thumb"));
        Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "medium"));
        Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "large"));
        foreach (var item in MediaFolders.Items)
        {
            Directory.CreateDirectory(Path.Combine(RootPath, "originals", item.Key));
            Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "icon", item.Key));
            Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "thumb", item.Key));
            Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "medium", item.Key));
            Directory.CreateDirectory(Path.Combine(RootPath, "optimized", "large", item.Key));
        }
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady)
            return;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "MediaAssets" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MediaAssets" PRIMARY KEY AUTOINCREMENT,
                "FileName" TEXT NOT NULL,
                "Alt" TEXT NULL,
                "Folder" TEXT NOT NULL DEFAULT 'khac',
                "IsImage" INTEGER NOT NULL,
                "OriginalPath" TEXT NOT NULL,
                "OriginalUrl" TEXT NOT NULL,
                "OriginalBytes" INTEGER NOT NULL,
                "ThumbPath" TEXT NULL,
                "ThumbUrl" TEXT NULL,
                "MediumPath" TEXT NULL,
                "MediumUrl" TEXT NULL,
                "LargePath" TEXT NULL,
                "LargeUrl" TEXT NULL,
                "ContentHash" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """, ct);

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "MediaAssets" ADD COLUMN "Folder" TEXT NOT NULL DEFAULT 'khac';""", ct);
        }
        catch
        {
            // Column already exists.
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "MediaAssets" ADD COLUMN "ContentHash" TEXT NULL;""", ct);
        }
        catch
        {
            // Column already exists.
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """CREATE INDEX IF NOT EXISTS "IX_MediaAssets_ContentHash" ON "MediaAssets" ("ContentHash");""", ct);
        }
        catch
        {
            // ignore
        }

        await SoftDeleteSchema.EnsureAsync(db, ct);
        await ImportLegacyFlatFilesAsync(db, ct);
        await RepairCorruptedAltsAsync(db, ct);
        _schemaReady = true;
    }

    /// <summary>
    /// Fixes Alt values corrupted to '?' (bad encoding) so SEO preview doesn't slug as g-m-an-...
    /// </summary>
    private static async Task RepairCorruptedAltsAsync(AppDbContext db, CancellationToken ct)
    {
        var broken = await db.MediaAssets
            .Where(item => item.Alt != null && (item.Alt.Contains("?") || item.Alt.Contains("\uFFFD")))
            .ToListAsync(ct);
        if (broken.Count == 0)
            return;

        foreach (var asset in broken)
        {
            var baseName = Path.GetFileNameWithoutExtension(asset.FileName);
            baseName = Regex.Replace(baseName ?? string.Empty, @"-\d{14}$", "");
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "anh";

            // Prefer readable Vietnamese for known page images; else title-case from slug.
            asset.Alt = baseName.ToLowerInvariant() switch
            {
                "gom-an-khang-gioi-thieu" => "Gốm An Khang giới thiệu",
                "gom-an-khang-gioi-thieu-2" => "Gốm An Khang ảnh 2",
                _ => CultureTitleFromSlug(baseName)
            };
            asset.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string CultureTitleFromSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return "Ảnh";
        var chars = slug.Replace('-', ' ').Replace('_', ' ').Trim().ToCharArray();
        var cap = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ' ')
            {
                cap = true;
                continue;
            }

            if (cap && char.IsLetter(chars[i]))
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                cap = false;
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// Promotes flat wwwroot/uploads/* files (used by old CKEditor content) into MediaAssets
    /// so SEO rename can update CustomPages/Posts content.
    /// </summary>
    private async Task ImportLegacyFlatFilesAsync(AppDbContext db, CancellationToken ct)
    {
        if (!Directory.Exists(RootPath))
            return;

        var existingUrls = await db.MediaAssets.AsNoTracking()
            .Select(item => item.OriginalUrl)
            .ToListAsync(ct);
        var known = new HashSet<string>(existingUrls, StringComparer.OrdinalIgnoreCase);

        var candidates = new List<FileInfo>();
        foreach (var path in Directory.EnumerateFiles(RootPath))
        {
            var info = new FileInfo(path);
            var ext = info.Extension;
            var isImage = ImageExt.Contains(ext) || PassThroughImageExt.Contains(ext);
            if (!isImage && !FileExt.Contains(ext))
                continue;
            var url = "/uploads/" + info.Name;
            if (known.Contains(url))
                continue;
            candidates.Add(info);
        }

        if (candidates.Count == 0)
            return;

        var pageContents = await db.CustomPages.AsNoTracking().Select(item => item.Content).ToListAsync(ct);
        var postContents = await db.Posts.AsNoTracking().Select(item => item.Content).ToListAsync(ct);
        var productDescs = await db.Products.AsNoTracking().Select(item => item.Description).ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var info in candidates)
        {
            var url = "/uploads/" + info.Name;
            var isImage = ImageExt.Contains(info.Extension) || PassThroughImageExt.Contains(info.Extension);
            var folder = MediaFolders.Other;
            if (pageContents.Any(html => html != null && html.Contains(url, StringComparison.OrdinalIgnoreCase)))
                folder = MediaFolders.Pages;
            else if (postContents.Any(html => html != null && html.Contains(url, StringComparison.OrdinalIgnoreCase)))
                folder = MediaFolders.News;
            else if (productDescs.Any(html => html != null && html.Contains(url, StringComparison.OrdinalIgnoreCase)))
                folder = MediaFolders.Products;

            var rawBase = Path.GetFileNameWithoutExtension(info.Name);
            // Avoid treating yyyyMMddHHmmss stamp as part of alt/slug.
            var altBase = Regex.Replace(rawBase, @"-\d{14}$", "");
            if (string.IsNullOrWhiteSpace(altBase))
                altBase = rawBase;

            db.MediaAssets.Add(new MediaAsset
            {
                FileName = info.Name,
                Alt = TextHelper.Clean(altBase.Replace('-', ' ').Replace('_', ' ')),
                Folder = folder,
                IsImage = isImage,
                OriginalPath = info.Name,
                OriginalUrl = url,
                OriginalBytes = info.Length,
                ThumbUrl = isImage ? url : null,
                MediumUrl = isImage ? url : null,
                LargeUrl = isImage ? url : null,
                CreatedAt = info.CreationTimeUtc,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<MediaPageResult> ListPagedAsync(MediaQuery query, CancellationToken ct = default)
    {
        EnsureRoot();
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var imagesOnly = string.Equals(query.Type, "Images", StringComparison.OrdinalIgnoreCase);
        var folder = MediaFolders.Normalize(query.Folder);
        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize > 0 ? query.PageSize : MediaFolders.PageSize;

        var baseQuery = db.MediaAssets.AsNoTracking().AsQueryable();
        if (imagesOnly)
            baseQuery = baseQuery.Where(item => item.IsImage);

        var folderCounts = await baseQuery
            .GroupBy(item => item.Folder)
            .Select(group => new { Folder = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [MediaFolders.All] = 0
        };
        foreach (var item in MediaFolders.Items)
            counts[item.Key] = 0;
        var totalAll = 0;
        foreach (var row in folderCounts)
        {
            var key = MediaFolders.Normalize(row.Folder);
            if (key == MediaFolders.All)
                key = MediaFolders.Other;
            counts[key] = counts.GetValueOrDefault(key) + row.Count;
            totalAll += row.Count;
        }

        var filtered = folder == MediaFolders.All
            ? baseQuery
            : baseQuery.Where(item => item.Folder == folder);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            filtered = filtered.Where(item =>
                item.FileName.Contains(term) ||
                (item.Alt != null && item.Alt.Contains(term)));
        }

        if (query.MissingAlt)
        {
            filtered = filtered.Where(item => item.Alt == null || item.Alt == "");
        }

        var totalCount = await filtered.CountAsync(ct);
        var assets = await filtered
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = assets.Select(ToItem).ToList();
        if (folder == MediaFolders.All && page == 1)
        {
            foreach (var legacy in ListLegacyFiles(imagesOnly))
            {
                if (items.Any(item => string.Equals(item.Url, legacy.Url, StringComparison.OrdinalIgnoreCase)))
                    continue;
                items.Insert(0, legacy);
                totalCount++;
                totalAll++;
                counts[MediaFolders.Other] = counts.GetValueOrDefault(MediaFolders.Other) + 1;
            }
        }

        counts[MediaFolders.All] = totalAll;
        return new MediaPageResult
        {
            Items = items,
            FolderCounts = counts,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<MediaItem>> ListAsync(string type = "Files", CancellationToken ct = default)
    {
        var page = await ListPagedAsync(new MediaQuery { Type = type, Folder = MediaFolders.All, Page = 1, PageSize = 5000 }, ct);
        return page.Items;
    }

    public IReadOnlyList<MediaItem> List(string type = "Images") =>
        ListAsync(type).GetAwaiter().GetResult();

    public Task<(bool Ok, string? Url, string? Error)> SaveAsync(IFormFile file, string type, CancellationToken ct) =>
        SaveCoreAsync(file, type, null, MediaFolders.Other, ct);

    public Task<(bool Ok, string? Url, string? Error, MediaAsset? Asset)> SaveAsync(
        IFormFile file, string type, string? alt, CancellationToken ct = default) =>
        SaveFullAsync(file, type, alt, MediaFolders.Other, ct);

    public Task<(bool Ok, string? Url, string? Error, MediaAsset? Asset)> SaveAsync(
        IFormFile file, string type, string? alt, string? folder, CancellationToken ct = default) =>
        SaveFullAsync(file, type, alt, folder, ct);

    private async Task<(bool Ok, string? Url, string? Error)> SaveCoreAsync(
        IFormFile file, string type, string? alt, string? folder, CancellationToken ct)
    {
        var result = await SaveFullAsync(file, type, alt, folder, ct);
        return (result.Ok, result.Url, result.Error);
    }

    private async Task<(bool Ok, string? Url, string? Error, MediaAsset? Asset)> SaveFullAsync(
        IFormFile file, string type, string? alt, string? folder, CancellationToken ct)
    {
        if (file.Length <= 0)
            return (false, null, "File trống.", null);
        if (file.Length > _options.MaxUploadBytes)
            return (false, null, $"File tối đa {_options.MaxUploadBytes / (1024 * 1024)}MB.", null);

        var ext = Path.GetExtension(file.FileName);
        var isImage = ImageExt.Contains(ext) || PassThroughImageExt.Contains(ext);

        if (string.Equals(type, "Images", StringComparison.OrdinalIgnoreCase) && !isImage)
            return (false, null, "Chỉ chấp nhận ảnh (jpg, png, gif, webp, svg…).", null);
        if (!isImage && !FileExt.Contains(ext))
            return (false, null, "Định dạng file không được hỗ trợ.", null);
        if (isImage && !ImageExt.Contains(ext) && !PassThroughImageExt.Contains(ext))
            return (false, null, "Định dạng ảnh không được hỗ trợ.", null);

        EnsureRoot();
        await EnsureSchemaAsync(ct);

        var now = DateTime.UtcNow;
        var stamp = now.ToString("yyyyMMddHHmmss");
        var ym = $"{now:yyyy}/{now:MM}";
        var safeBase = SanitizeName(Path.GetFileNameWithoutExtension(file.FileName));
        if (string.IsNullOrWhiteSpace(safeBase))
            safeBase = "file";

        var folderKey = MediaFolders.Normalize(folder);
        if (folderKey == MediaFolders.All)
            folderKey = MediaFolders.Other;

        var originalName = $"{safeBase}-{stamp}{ext.ToLowerInvariant()}";
        var originalRel = Path.Combine(
            "originals",
            folderKey,
            ym.Replace('/', Path.DirectorySeparatorChar),
            originalName);
        var originalAbs = Path.Combine(RootPath, originalRel);
        Directory.CreateDirectory(Path.GetDirectoryName(originalAbs)!);

        string contentHash;
        await using (var input = file.OpenReadStream())
        await using (var output = File.Create(originalAbs))
            contentHash = await MediaHash.Sha256HexCopyAsync(input, output, ct);

        if (_options.DedupeOnUpload && !string.IsNullOrWhiteSpace(contentHash))
        {
            using var dedupeScope = scopes.CreateScope();
            var dedupeDb = dedupeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await dedupeDb.MediaAssets.AsNoTracking()
                .FirstOrDefaultAsync(item => item.ContentHash == contentHash, ct);
            if (existing is not null)
            {
                TryDelete(originalAbs);
                var publicExisting = existing.MediumUrl ?? existing.OriginalUrl;
                return (true, publicExisting, null, existing);
            }
        }

        var originalUrl = ToUrl(originalRel);
        var asset = new MediaAsset
        {
            FileName = originalName,
            Alt = TextHelper.Clean(alt) ?? TextHelper.Clean(safeBase.Replace('-', ' ')),
            Folder = folderKey,
            IsImage = isImage,
            OriginalPath = originalRel.Replace('\\', '/'),
            OriginalUrl = originalUrl,
            OriginalBytes = file.Length,
            ContentHash = contentHash,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (isImage && !PassThroughImageExt.Contains(ext))
        {
            try
            {
                await WriteOptimizedAsync(originalAbs, safeBase, stamp, ym, folderKey, asset, ct);
            }
            catch (Exception ex)
            {
                // Keep original even if optimize fails.
                asset.ThumbUrl = originalUrl;
                asset.MediumUrl = originalUrl;
                asset.LargeUrl = originalUrl;
                asset.Alt = (asset.Alt ?? safeBase) + $" (tối ưu lỗi: {ex.Message})";
            }
        }
        else if (isImage)
        {
            asset.ThumbUrl = originalUrl;
            asset.MediumUrl = originalUrl;
            asset.LargeUrl = originalUrl;
        }

        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MediaAssets.Add(asset);
            await db.SaveChangesAsync(ct);
        }

        var publicUrl = asset.MediumUrl ?? asset.OriginalUrl;
        return (true, publicUrl, null, asset);
    }

    public async Task<(bool Ok, string? Url, string? Error, MediaAsset? Asset)> ImportFromUrlAsync(
        HttpClient http,
        string remoteUrl,
        string? alt = null,
        MediaSize prefer = MediaSize.Medium,
        string? folder = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return (false, null, "URL trống.", null);
        if (remoteUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return (true, remoteUrl, null, null);
        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return (false, null, "URL không hợp lệ.", null);

        EnsureRoot();
        await EnsureSchemaAsync(ct);

        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            return (false, null, $"Tải thất bại ({(int)response.StatusCode}).", null);

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0)
            return (false, null, "File trống.", null);
        if (bytes.Length > _options.MaxUploadBytes)
            return (false, null, "File tối đa 15MB.", null);

        var ext = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            ext = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/svg+xml" => ".svg",
                "image/bmp" => ".bmp",
                _ => ".jpg"
            };
        }

        var nameFromUrl = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(nameFromUrl))
            nameFromUrl = "remote";
        var fileName = SanitizeName(nameFromUrl) + ext.ToLowerInvariant();

        await using var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
        };

        var saved = await SaveFullAsync(formFile, "Images", alt, folder, ct);
        if (!saved.Ok || saved.Asset is null)
            return saved;

        var url = prefer switch
        {
            MediaSize.Thumb => saved.Asset.ThumbUrl,
            MediaSize.Large => saved.Asset.LargeUrl,
            MediaSize.Original => saved.Asset.OriginalUrl,
            _ => saved.Asset.MediumUrl
        } ?? saved.Url;

        return (true, url, null, saved.Asset);
    }

    public async Task<(bool Ok, string? Error)> UpdateAltAsync(int id, string? alt, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return (false, "Không tìm thấy ảnh.");
        asset.Alt = TextHelper.Clean(alt);
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateFolderAsync(int id, string? folder, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return (false, "Không tìm thấy ảnh.");

        var key = MediaFolders.Normalize(folder);
        if (key == MediaFolders.All)
            key = MediaFolders.Other;
        asset.Folder = key;
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RegenerateAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return (false, "Không tìm thấy ảnh.");
        if (!asset.IsImage)
            return (false, "Chỉ tái tạo được với ảnh.");

        var originalAbs = Path.Combine(RootPath, asset.OriginalPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(originalAbs))
            return (false, "Thiếu file gốc.");

        if (string.IsNullOrWhiteSpace(asset.ContentHash))
        {
            try
            {
                await using var hashStream = File.OpenRead(originalAbs);
                asset.ContentHash = await MediaHash.Sha256HexAsync(hashStream, ct);
            }
            catch
            {
                // ignore hash failures
            }
        }

        var ext = Path.GetExtension(asset.OriginalPath);
        if (PassThroughImageExt.Contains(ext))
        {
            asset.ThumbUrl = asset.OriginalUrl;
            asset.MediumUrl = asset.OriginalUrl;
            asset.LargeUrl = asset.OriginalUrl;
            asset.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return (true, null);
        }

        var oldThumbUrl = asset.ThumbUrl;
        var oldMediumUrl = asset.MediumUrl;
        var oldLargeUrl = asset.LargeUrl;

        DeleteOptimizedFiles(asset);
        var (safeBase, stamp, ym, folderKey) = ResolveOptimizedNaming(asset);
        try
        {
            await WriteOptimizedAsync(originalAbs, safeBase, stamp, ym, folderKey, asset, ct);
        }
        catch (Exception ex)
        {
            return (false, "Tối ưu thất bại: " + ex.Message);
        }

        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddUrlMap(map, oldThumbUrl, asset.ThumbUrl);
        AddUrlMap(map, oldMediumUrl, asset.MediumUrl);
        AddUrlMap(map, oldLargeUrl, asset.LargeUrl);
        // icon URLs derived from thumb/medium paths
        if (!string.IsNullOrWhiteSpace(oldThumbUrl) && !string.IsNullOrWhiteSpace(asset.ThumbUrl))
            AddUrlMap(map, MediaUrls.For(oldThumbUrl, MediaSize.Icon), MediaUrls.For(asset.ThumbUrl, MediaSize.Icon));
        if (!string.IsNullOrWhiteSpace(oldMediumUrl) && !string.IsNullOrWhiteSpace(asset.MediumUrl))
            AddUrlMap(map, MediaUrls.For(oldMediumUrl, MediaSize.Icon), MediaUrls.For(asset.MediumUrl, MediaSize.Icon));

        if (map.Count > 0)
            await RewriteUrlMapAsync(db, map, asset.Alt ?? string.Empty, ct);

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RegenerateAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.MediaAssets.Where(item => item.IsImage).Select(item => item.Id).ToListAsync(ct);
        var failed = 0;
        foreach (var id in ids)
        {
            var (ok, _) = await RegenerateAsync(id, ct);
            if (!ok) failed++;
        }

        var repaired = await RepairDriftedOptimizedUrlsAsync(ct);
        var note = failed == 0
            ? null
            : $"Đã chạy xong, {failed} ảnh lỗi.";
        if (repaired > 0)
            note = string.IsNullOrWhiteSpace(note)
                ? $"Đã remap {repaired} tham chiếu URL ảnh."
                : note + $" Remap {repaired} tham chiếu.";

        return (true, note);
    }

    private static void AddUrlMap(Dictionary<string, string> map, string? oldUrl, string? newUrl)
    {
        if (string.IsNullOrWhiteSpace(oldUrl) || string.IsNullOrWhiteSpace(newUrl))
            return;
        if (string.Equals(oldUrl, newUrl, StringComparison.OrdinalIgnoreCase))
            return;
        map[oldUrl] = newUrl;
    }

    public async Task<bool> DeleteAsync(int id, string? deletedBy = null, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return false;

        SoftDelete.Mark(asset, deletedBy);
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RestoreAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.MediaAssets.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == id && item.IsDeleted, ct);
        if (asset is null)
            return false;

        SoftDelete.Restore(asset);
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> HardDeleteAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.MediaAssets.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return false;

        TryDelete(Path.Combine(RootPath, asset.OriginalPath.Replace('/', Path.DirectorySeparatorChar)));
        DeleteOptimizedFiles(asset);
        db.MediaAssets.Remove(asset);
        await db.SaveChangesAsync(ct);
        // Hashed on-the-fly cache cannot target one source file reliably.
        MediaImageCache.PurgeAll(env);
        return true;
    }

    public bool Delete(string fileName)
    {
        // Legacy flat delete
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            return false;
        var path = Path.Combine(RootPath, name);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private async Task WriteOptimizedAsync(
        string originalAbs,
        string safeBase,
        string stamp,
        string ym,
        string folderKey,
        MediaAsset asset,
        CancellationToken ct)
    {
        await using var input = File.OpenRead(originalAbs);
        using var image = await Image.LoadAsync(input, ct);

        // icon: nhỏ cho menu/danh mục; thumb: crop vuông cho card
        _ = await SaveVariantAsync(image, safeBase, stamp, ym, folderKey, "icon", _options.IconWidth, squareCrop: true, asset, setPath: null, ct);
        asset.ThumbUrl = await SaveVariantAsync(image, safeBase, stamp, ym, folderKey, "thumb", _options.ThumbWidth, squareCrop: true, asset, setPath: (a, p, u) => { a.ThumbPath = p; a.ThumbUrl = u; }, ct);
        asset.MediumUrl = await SaveVariantAsync(image, safeBase, stamp, ym, folderKey, "medium", _options.MediumWidth, squareCrop: false, asset, setPath: (a, p, u) => { a.MediumPath = p; a.MediumUrl = u; }, ct);
        asset.LargeUrl = await SaveVariantAsync(image, safeBase, stamp, ym, folderKey, "large", _options.LargeWidth, squareCrop: false, asset, setPath: (a, p, u) => { a.LargePath = p; a.LargeUrl = u; }, ct);
    }

    private async Task<string> SaveVariantAsync(
        Image image,
        string safeBase,
        string stamp,
        string ym,
        string folderKey,
        string sizeFolder,
        int maxWidth,
        bool squareCrop,
        MediaAsset asset,
        Action<MediaAsset, string, string>? setPath,
        CancellationToken ct)
    {
        var rel = Path.Combine(
            "optimized",
            sizeFolder,
            folderKey,
            ym.Replace('/', Path.DirectorySeparatorChar),
            $"{safeBase}-{stamp}.webp");
        var abs = Path.Combine(RootPath, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);

        using var clone = image.Clone(ctx =>
        {
            if (squareCrop)
            {
                ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Crop,
                    Size = new Size(maxWidth, maxWidth),
                    Position = AnchorPositionMode.Center
                });
            }
            else if (image.Width > maxWidth || image.Height > maxWidth * 2)
            {
                ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, maxWidth)
                });
            }
        });

        var encoder = new WebpEncoder { Quality = _options.WebpQuality };
        await clone.SaveAsync(abs, encoder, ct);
        var url = ToUrl(rel);
        setPath?.Invoke(asset, rel.Replace('\\', '/'), url);
        return url;
    }

    private void DeleteOptimizedFiles(MediaAsset asset)
    {
        if (!string.IsNullOrWhiteSpace(asset.ThumbPath))
        {
            var thumbAbs = Path.Combine(RootPath, asset.ThumbPath.Replace('/', Path.DirectorySeparatorChar));
            TryDelete(thumbAbs);
            // icon cùng tên, đổi folder thumb → icon
            var iconRel = asset.ThumbPath.Replace("/optimized/thumb/", "/optimized/icon/", StringComparison.OrdinalIgnoreCase)
                .Replace("\\optimized\\thumb\\", "\\optimized\\icon\\", StringComparison.OrdinalIgnoreCase);
            TryDelete(Path.Combine(RootPath, iconRel.Replace('/', Path.DirectorySeparatorChar)));
        }
        if (!string.IsNullOrWhiteSpace(asset.MediumPath))
            TryDelete(Path.Combine(RootPath, asset.MediumPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!string.IsNullOrWhiteSpace(asset.LargePath))
            TryDelete(Path.Combine(RootPath, asset.LargePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>Reuse existing optimized naming so product/post URLs stay valid after regenerate.</summary>
    private static (string SafeBase, string Stamp, string Ym, string FolderKey) ResolveOptimizedNaming(MediaAsset asset)
    {
        var sample = asset.ThumbPath ?? asset.MediumPath ?? asset.LargePath
                     ?? asset.ThumbUrl ?? asset.MediumUrl ?? asset.LargeUrl;
        if (!string.IsNullOrWhiteSpace(sample))
        {
            var norm = sample.Replace('\\', '/').Trim();
            // URLs may be stored as /uploads/optimized/...
            if (norm.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                norm = norm["/uploads/".Length..];
            else if (norm.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                norm = norm["uploads/".Length..];

            var parts = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // optimized / {size} / {folder} / yyyy / MM / file.webp
            // or legacy: optimized / {size} / yyyy / MM / file.webp
            if (parts.Length >= 5 && parts[0].Equals("optimized", StringComparison.OrdinalIgnoreCase))
            {
                string folderKey;
                string ym;
                string file;
                if (parts.Length >= 6 && Regex.IsMatch(parts[3], @"^\d{4}$") && Regex.IsMatch(parts[4], @"^\d{2}$"))
                {
                    folderKey = MediaFolders.Normalize(parts[2]);
                    if (folderKey == MediaFolders.All)
                        folderKey = MediaFolders.Other;
                    ym = $"{parts[3]}/{parts[4]}";
                    file = Path.GetFileNameWithoutExtension(parts[^1]);
                }
                else if (Regex.IsMatch(parts[2], @"^\d{4}$") && Regex.IsMatch(parts[3], @"^\d{2}$"))
                {
                    folderKey = MediaFolders.Normalize(asset.Folder);
                    if (folderKey == MediaFolders.All)
                        folderKey = MediaFolders.Other;
                    ym = $"{parts[2]}/{parts[3]}";
                    file = Path.GetFileNameWithoutExtension(parts[^1]);
                }
                else
                {
                    folderKey = string.Empty;
                    ym = string.Empty;
                    file = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(file))
                {
                    var m = Regex.Match(file, @"^(.*)-(\d{14})$");
                    if (m.Success)
                        return (m.Groups[1].Value, m.Groups[2].Value, ym, folderKey);
                }
            }
        }

        // Strip trailing stamp(s) before sanitize so we don't append a second stamp.
        var rawBase = Path.GetFileNameWithoutExtension(asset.FileName) ?? "file";
        while (Regex.IsMatch(rawBase, @"-\d{14}$"))
            rawBase = Regex.Replace(rawBase, @"-\d{14}$", string.Empty);
        var safeBase = SanitizeName(rawBase);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fallbackYm = $"{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}";
        var fallbackFolder = MediaFolders.Normalize(asset.Folder);
        if (fallbackFolder == MediaFolders.All)
            fallbackFolder = MediaFolders.Other;
        return (safeBase, stamp, fallbackYm, fallbackFolder);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private IEnumerable<MediaItem> ListLegacyFiles(bool imagesOnly)
    {
        if (!Directory.Exists(RootPath))
            yield break;

        foreach (var path in Directory.EnumerateFiles(RootPath))
        {
            var info = new FileInfo(path);
            var ext = info.Extension;
            var isImage = ImageExt.Contains(ext) || PassThroughImageExt.Contains(ext);
            if (imagesOnly && !isImage)
                continue;
            if (!imagesOnly && !FileExt.Contains(ext) && !isImage)
                continue;

            yield return new MediaItem(
                0,
                info.Name,
                $"/uploads/{info.Name}",
                $"/uploads/{info.Name}",
                $"/uploads/{info.Name}",
                $"/uploads/{info.Name}",
                $"/uploads/{info.Name}",
                null,
                MediaFolders.Other,
                info.Length,
                info.LastWriteTimeUtc,
                isImage,
                true);
        }
    }

    private static MediaItem ToItem(MediaAsset asset) => new(
        asset.Id,
        asset.FileName,
        asset.OriginalUrl,
        asset.ThumbUrl ?? asset.OriginalUrl,
        asset.MediumUrl ?? asset.OriginalUrl,
        asset.LargeUrl ?? asset.OriginalUrl,
        asset.MediumUrl ?? asset.OriginalUrl,
        asset.Alt,
        MediaFolders.Normalize(asset.Folder) == MediaFolders.All ? MediaFolders.Other : MediaFolders.Normalize(asset.Folder),
        asset.OriginalBytes,
        asset.UpdatedAt,
        asset.IsImage,
        false);

    private static string ToUrl(string relativePath) =>
        "/uploads/" + relativePath.Replace('\\', '/');

    private static string SanitizeName(string value)
    {
        var slug = TextHelper.Slug(value, "file");
        if (slug.Length > 60)
            slug = slug[..60].Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "file" : slug;
    }
}

public sealed record MediaItem(
    int Id,
    string Name,
    string OriginalUrl,
    string ThumbUrl,
    string MediumUrl,
    string LargeUrl,
    string Url,
    string? Alt,
    string Folder,
    long Size,
    DateTime ModifiedUtc,
    bool IsImage,
    bool IsLegacy);
