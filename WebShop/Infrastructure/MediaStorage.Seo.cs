using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public sealed record MediaSeoPreviewItem(
    int Id,
    string ThumbUrl,
    string OldName,
    string NewName,
    string? OldAlt,
    string NewAlt,
    string Source,
    bool WillRename,
    bool WillUpdateAlt,
    string? Note);

public sealed record MediaSeoApplyItem(int Id, string NewBaseName, string? NewAlt);

public sealed record MediaSeoApplyResult(int Renamed, int AltUpdated, int RefsUpdated, int Skipped, int Failed, string Message);

public sealed partial class MediaStorage
{
    private static readonly Regex StampSuffix = new(@"-(\d{14})$", RegexOptions.Compiled);

    /// <summary>
    /// Rewrite product/post/slide content URLs after optimized paths change.
    /// </summary>
    public async Task<int> RewriteUrlMapAsync(
        AppDbContext db,
        Dictionary<string, string> urlMap,
        string newAlt,
        CancellationToken ct = default)
    {
        if (urlMap.Count == 0)
            return 0;
        var oldUrls = urlMap.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await RewriteReferencesAsync(db, urlMap, oldUrls, newAlt, ct);
    }

    /// <summary>
    /// Fix content still pointing at pre-regen paths by matching filenames to current MediaAsset URLs
    /// (handles missing folder segment, ym drift, and double-stamp renames).
    /// </summary>
    public async Task<int> RepairDriftedOptimizedUrlsAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assets = await db.MediaAssets
            .Where(item => item.IsImage && item.MediumUrl != null)
            .Select(item => new { item.ThumbUrl, item.MediumUrl, item.LargeUrl, item.FileName })
            .ToListAsync(ct);

        // filename / prior-stamp filename → current variants
        var byFile = new Dictionary<string, (string? Thumb, string Medium, string? Large)>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            var medium = asset.MediumUrl!;
            var entry = (asset.ThumbUrl, medium, asset.LargeUrl);
            foreach (var key in FilenameMatchKeys(Path.GetFileName(medium)))
                byFile[key] = entry;
            if (!string.IsNullOrWhiteSpace(asset.ThumbUrl))
            {
                foreach (var key in FilenameMatchKeys(Path.GetFileName(asset.ThumbUrl)))
                    byFile[key] = entry;
            }

            if (!string.IsNullOrWhiteSpace(asset.FileName))
            {
                foreach (var key in FilenameMatchKeys(asset.FileName))
                    byFile[key] = entry;
            }
        }

        var usage = scope.ServiceProvider.GetRequiredService<MediaUsageService>();
        var contentUrls = (await usage.CollectReferencedUrlsAsync(ct))
            .Where(url => url.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase));
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in contentUrls)
        {
            var file = Path.GetFileName(url);
            if (string.IsNullOrWhiteSpace(file))
                continue;

            (string? Thumb, string Medium, string? Large) entry = default;
            var found = false;
            foreach (var key in FilenameMatchKeys(file))
            {
                if (byFile.TryGetValue(key, out entry))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                continue;

            var target = ResolveSizeUrl(url, entry.Thumb, entry.Medium, entry.Large);
            AddUrlMap(map, url, target);
        }

        if (map.Count == 0)
            return 0;

        return await RewriteUrlMapAsync(db, map, string.Empty, ct);
    }

    private static IEnumerable<string> FilenameMatchKeys(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        var file = Path.GetFileName(fileName);
        yield return file;

        var noExt = Path.GetFileNameWithoutExtension(file);
        var ext = Path.GetExtension(file);
        if (string.IsNullOrWhiteSpace(noExt))
            yield break;

        // Stamps may be truncated (SanitizeName 60-char limit) → allow 8–14 digits.
        var stamps = new List<string>();
        var core = noExt;
        while (true)
        {
            var m = Regex.Match(core, @"-(\d{8,14})$");
            if (!m.Success)
                break;
            stamps.Insert(0, m.Groups[1].Value);
            core = core[..^m.Length];
        }

        if (stamps.Count >= 2)
            yield return $"{core}-{stamps[0]}{ext}";
        if (stamps.Count >= 1)
            yield return $"{core}{ext}";
    }

    private static string ResolveSizeUrl(string sourceUrl, string? thumb, string medium, string? large)
    {
        if (sourceUrl.Contains("/optimized/icon/", StringComparison.OrdinalIgnoreCase))
            return MediaUrls.For(medium, MediaSize.Icon);
        if (sourceUrl.Contains("/optimized/thumb/", StringComparison.OrdinalIgnoreCase))
            return thumb ?? MediaUrls.For(medium, MediaSize.Thumb);
        if (sourceUrl.Contains("/optimized/large/", StringComparison.OrdinalIgnoreCase))
            return large ?? MediaUrls.For(medium, MediaSize.Large);
        return medium;
    }

    public async Task<IReadOnlyList<MediaSeoPreviewItem>> PreviewSeoOptimizeAsync(
        IReadOnlyList<int> ids,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var idList = ids.Where(id => id > 0).Distinct().Take(100).ToList();
        if (idList.Count == 0)
            return [];

        var assets = await db.MediaAssets.AsNoTracking()
            .Where(item => idList.Contains(item.Id) && item.IsImage)
            .ToListAsync(ct);

        var products = await db.Products.AsNoTracking()
            .Select(item => new { item.Id, item.Name, item.Slug, item.ImageUrl, item.SeoImage })
            .ToListAsync(ct);
        var productImages = await db.ProductImages.AsNoTracking()
            .Select(item => new { item.ProductId, item.ImageUrl, item.AltText })
            .ToListAsync(ct);
        var posts = await db.Posts.AsNoTracking()
            .Select(item => new { item.Title, item.Slug, item.ImageUrl, item.SeoImage })
            .ToListAsync(ct);

        var productById = products.ToDictionary(item => item.Id);
        var usedBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in await db.MediaAssets.AsNoTracking().Select(item => item.FileName).ToListAsync(ct))
        {
            var baseName = BaseNameWithoutStamp(Path.GetFileNameWithoutExtension(existing));
            if (!string.IsNullOrWhiteSpace(baseName))
                usedBases.Add(baseName);
        }

        var ordered = idList
            .Select(id => assets.FirstOrDefault(item => item.Id == id))
            .Where(item => item is not null)
            .Cast<MediaAsset>()
            .ToList();

        var result = new List<MediaSeoPreviewItem>(ordered.Count);
        foreach (var asset in ordered)
        {
            var urls = UrlSet(asset);
            string source;
            string suggestedBase;
            string? suggestedAlt = null;

            var linkedProduct = products.FirstOrDefault(item =>
                UrlMatches(item.ImageUrl, urls) || UrlMatches(item.SeoImage, urls));
            if (linkedProduct is null)
            {
                var album = productImages.FirstOrDefault(item => UrlMatches(item.ImageUrl, urls));
                if (album is not null && productById.TryGetValue(album.ProductId, out var parent))
                {
                    linkedProduct = parent;
                    if (!string.IsNullOrWhiteSpace(album.AltText))
                        suggestedAlt = album.AltText.Trim();
                }
            }

            if (linkedProduct is not null)
            {
                source = "Sản phẩm";
                suggestedBase = SanitizeName(linkedProduct.Slug);
                suggestedAlt = TextHelper.Clean(linkedProduct.Name)
                    ?? suggestedAlt
                    ?? TitleFromSlug(suggestedBase);
            }
            else
            {
                var linkedPost = posts.FirstOrDefault(item =>
                    UrlMatches(item.ImageUrl, urls) || UrlMatches(item.SeoImage, urls));
                if (linkedPost is not null)
                {
                    source = "Bài viết";
                    suggestedBase = SanitizeName(linkedPost.Slug);
                    suggestedAlt = TextHelper.Clean(linkedPost.Title) ?? TitleFromSlug(suggestedBase);
                }
                else if (IsUsefulAlt(asset.Alt))
                {
                    source = "Alt";
                    suggestedBase = SanitizeName(asset.Alt!);
                    suggestedAlt = asset.Alt!.Trim();
                }
                else
                {
                    source = "Tên file";
                    suggestedBase = SanitizeName(BaseNameWithoutStamp(Path.GetFileNameWithoutExtension(asset.FileName)));
                    suggestedAlt = TitleFromSlug(suggestedBase);
                }
            }

            if (string.IsNullOrWhiteSpace(suggestedBase))
                suggestedBase = "anh";

            // Slug from alt/file must not keep the trailing upload stamp.
            suggestedBase = BaseNameWithoutStamp(suggestedBase);
            if (string.IsNullOrWhiteSpace(suggestedBase))
                suggestedBase = "anh";

            var uniqueBase = AllocateUniqueBase(suggestedBase, usedBases, BaseNameWithoutStamp(Path.GetFileNameWithoutExtension(asset.FileName)));
            var (_, stamp) = SplitBaseAndStamp(Path.GetFileNameWithoutExtension(asset.FileName));
            var ext = Path.GetExtension(asset.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = Path.GetExtension(asset.OriginalPath);
            var newFileName = $"{uniqueBase}-{stamp}{ext.ToLowerInvariant()}";

            var currentBase = BaseNameWithoutStamp(Path.GetFileNameWithoutExtension(asset.FileName));
            var willRename = !string.Equals(currentBase, uniqueBase, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(asset.FileName, newFileName, StringComparison.OrdinalIgnoreCase);

            var newAlt = suggestedAlt;
            if (!IsUsefulAlt(newAlt))
                newAlt = TitleFromSlug(uniqueBase);

            var willUpdateAlt = !string.Equals((asset.Alt ?? string.Empty).Trim(), newAlt!.Trim(), StringComparison.Ordinal);

            string? note = null;
            if (!willRename && !willUpdateAlt)
                note = "Đã chuẩn";
            else if (!willRename)
                note = "Chỉ cập nhật alt";

            result.Add(new MediaSeoPreviewItem(
                asset.Id,
                asset.ThumbUrl ?? asset.OriginalUrl,
                asset.FileName,
                newFileName,
                asset.Alt,
                newAlt,
                source,
                willRename,
                willUpdateAlt,
                note));
        }

        return result;
    }

    public async Task<MediaSeoApplyResult> ApplySeoOptimizeAsync(
        IReadOnlyList<MediaSeoApplyItem> items,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        EnsureRoot();

        var renamed = 0;
        var altUpdated = 0;
        var refsUpdated = 0;
        var skipped = 0;
        var failed = 0;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var usedBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in await db.MediaAssets.AsNoTracking().Select(item => item.FileName).ToListAsync(ct))
        {
            var baseName = BaseNameWithoutStamp(Path.GetFileNameWithoutExtension(existing));
            if (!string.IsNullOrWhiteSpace(baseName))
                usedBases.Add(baseName);
        }

        foreach (var item in items.Where(row => row.Id > 0).Take(100))
        {
            var asset = await db.MediaAssets.FirstOrDefaultAsync(row => row.Id == item.Id, ct);
            if (asset is null || !asset.IsImage)
            {
                failed++;
                continue;
            }

            var oldUrls = UrlSet(asset);
            var desiredBase = BaseNameWithoutStamp(SanitizeName(item.NewBaseName));
            if (string.IsNullOrWhiteSpace(desiredBase))
                desiredBase = "anh";

            var currentBase = BaseNameWithoutStamp(Path.GetFileNameWithoutExtension(asset.FileName));
            usedBases.Remove(currentBase);
            var uniqueBase = AllocateUniqueBase(desiredBase, usedBases, currentBase);
            usedBases.Add(uniqueBase);

            var (_, stamp) = SplitBaseAndStamp(Path.GetFileNameWithoutExtension(asset.FileName));
            var ext = Path.GetExtension(asset.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = Path.GetExtension(asset.OriginalPath);

            var newOriginalName = $"{uniqueBase}-{stamp}{ext.ToLowerInvariant()}";
            var willRename = !string.Equals(asset.FileName, newOriginalName, StringComparison.OrdinalIgnoreCase);
            var newAlt = TextHelper.Clean(item.NewAlt) ?? TitleFromSlug(uniqueBase);
            var willAlt = !string.Equals((asset.Alt ?? string.Empty).Trim(), newAlt, StringComparison.Ordinal);

            if (!willRename && !willAlt)
            {
                skipped++;
                continue;
            }

            try
            {
                Dictionary<string, string>? urlMap = null;
                if (willRename)
                {
                    urlMap = RenameAssetFiles(asset, uniqueBase, stamp, newOriginalName);
                    renamed++;
                }

                if (willAlt)
                {
                    asset.Alt = newAlt;
                    altUpdated++;
                }

                asset.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                if (urlMap is { Count: > 0 })
                    refsUpdated += await RewriteReferencesAsync(db, urlMap, oldUrls, newAlt, ct);
            }
            catch
            {
                failed++;
            }
        }

        var message = $"Đổi tên {renamed}, alt {altUpdated}, cập nhật link {refsUpdated}, bỏ qua {skipped}, lỗi {failed}.";
        return new MediaSeoApplyResult(renamed, altUpdated, refsUpdated, skipped, failed, message);
    }

    private Dictionary<string, string> RenameAssetFiles(
        MediaAsset asset,
        string newBase,
        string stamp,
        string newOriginalName)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var oldOriginalUrl = asset.OriginalUrl;
        var oldThumbUrl = asset.ThumbUrl;
        var oldMediumUrl = asset.MediumUrl;
        var oldLargeUrl = asset.LargeUrl;

        asset.OriginalPath = MoveRelativeFile(asset.OriginalPath, newOriginalName);
        asset.OriginalUrl = ToUrl(asset.OriginalPath);
        asset.FileName = newOriginalName;
        map[oldOriginalUrl] = asset.OriginalUrl;

        if (!string.IsNullOrWhiteSpace(asset.ThumbPath))
        {
            var name = $"{newBase}-{ExtractStampOrDefault(asset.ThumbPath, stamp)}.webp";
            asset.ThumbPath = MoveRelativeFile(asset.ThumbPath, name);
            asset.ThumbUrl = ToUrl(asset.ThumbPath);
            if (!string.IsNullOrWhiteSpace(oldThumbUrl))
                map[oldThumbUrl] = asset.ThumbUrl;
        }

        if (!string.IsNullOrWhiteSpace(asset.MediumPath))
        {
            var name = $"{newBase}-{ExtractStampOrDefault(asset.MediumPath, stamp)}.webp";
            asset.MediumPath = MoveRelativeFile(asset.MediumPath, name);
            asset.MediumUrl = ToUrl(asset.MediumPath);
            if (!string.IsNullOrWhiteSpace(oldMediumUrl))
                map[oldMediumUrl] = asset.MediumUrl;
        }

        if (!string.IsNullOrWhiteSpace(asset.LargePath))
        {
            var name = $"{newBase}-{ExtractStampOrDefault(asset.LargePath, stamp)}.webp";
            asset.LargePath = MoveRelativeFile(asset.LargePath, name);
            asset.LargeUrl = ToUrl(asset.LargePath);
            if (!string.IsNullOrWhiteSpace(oldLargeUrl))
                map[oldLargeUrl] = asset.LargeUrl;
        }

        return map;
    }

    private string MoveRelativeFile(string relativePath, string newFileName)
    {
        var rel = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var abs = Path.Combine(RootPath, rel);
        var dir = Path.GetDirectoryName(abs)!;
        var newAbs = Path.Combine(dir, newFileName);
        if (!File.Exists(abs))
            throw new FileNotFoundException("Thiếu file: " + relativePath);
        if (string.Equals(abs, newAbs, StringComparison.OrdinalIgnoreCase))
            return relativePath.Replace('\\', '/');

        if (File.Exists(newAbs))
            throw new IOException("File đích đã tồn tại: " + newFileName);

        Directory.CreateDirectory(dir);
        File.Move(abs, newAbs);
        var newRel = Path.Combine(Path.GetDirectoryName(rel)!, newFileName);
        return newRel.Replace('\\', '/');
    }

    private static async Task<int> RewriteReferencesAsync(
        AppDbContext db,
        Dictionary<string, string> urlMap,
        HashSet<string> oldUrls,
        string newAlt,
        CancellationToken ct)
    {
        var count = 0;
        var ordered = ExpandUrlMap(urlMap).OrderByDescending(item => item.Key.Length).ToList();
        var fileRenames = ordered
            .Select(pair => (
                OldFile: Path.GetFileName(pair.Key),
                NewFile: Path.GetFileName(pair.Value)))
            .Where(pair =>
                !string.IsNullOrWhiteSpace(pair.OldFile)
                && !string.IsNullOrWhiteSpace(pair.NewFile)
                && !string.Equals(pair.OldFile, pair.NewFile, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(pair => pair.OldFile, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(pair => pair.OldFile.Length)
            .ToList();

        string? ReplaceAll(string? value)
        {
            if (value is null)
                return null;
            if (value.Length == 0)
                return value;

            var next = value;
            foreach (var pair in ordered)
            {
                next = next.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
                // Absolute URLs: http(s)://host/uploads/...
                next = Regex.Replace(
                    next,
                    @"(https?://[^/""'\s]+)(" + Regex.Escape(pair.Key) + ")",
                    m => m.Groups[1].Value + pair.Value,
                    RegexOptions.IgnoreCase);
            }

            foreach (var pair in fileRenames)
                next = next.Replace(pair.OldFile, pair.NewFile, StringComparison.OrdinalIgnoreCase);

            return next;
        }

        foreach (var product in await db.Products.ToListAsync(ct))
        {
            var changed = false;
            var image = ReplaceAll(product.ImageUrl);
            if (image != product.ImageUrl) { product.ImageUrl = image; changed = true; }
            var seo = ReplaceAll(product.SeoImage);
            if (seo != product.SeoImage) { product.SeoImage = string.IsNullOrWhiteSpace(seo) ? null : seo; changed = true; }
            var desc = ReplaceAll(product.Description);
            if (desc != product.Description) { product.Description = desc; changed = true; }
            var shortDesc = ReplaceAll(product.ShortDescription);
            if (shortDesc != product.ShortDescription) { product.ShortDescription = string.IsNullOrWhiteSpace(shortDesc) ? null : shortDesc; changed = true; }
            if (changed) count++;
        }

        foreach (var image in await db.ProductImages.ToListAsync(ct))
        {
            var wasLinked = oldUrls.Contains(image.ImageUrl);
            var next = ReplaceAll(image.ImageUrl);
            var changed = false;
            if (!string.Equals(next, image.ImageUrl, StringComparison.Ordinal))
            {
                image.ImageUrl = next;
                changed = true;
            }

            if ((wasLinked || changed) && !IsUsefulAlt(image.AltText))
            {
                image.AltText = newAlt;
                changed = true;
            }

            if (changed) count++;
        }

        foreach (var post in await db.Posts.ToListAsync(ct))
        {
            var changed = false;
            var image = ReplaceAll(post.ImageUrl);
            if (image != post.ImageUrl) { post.ImageUrl = string.IsNullOrWhiteSpace(image) ? null : image; changed = true; }
            var seo = ReplaceAll(post.SeoImage);
            if (seo != post.SeoImage) { post.SeoImage = string.IsNullOrWhiteSpace(seo) ? null : seo; changed = true; }
            var content = ReplaceAll(post.Content);
            if (content != post.Content) { post.Content = content; changed = true; }
            if (changed) count++;
        }

        foreach (var page in await db.CustomPages.ToListAsync(ct))
        {
            var changed = false;
            var seo = ReplaceAll(page.SeoImage);
            if (seo != page.SeoImage) { page.SeoImage = string.IsNullOrWhiteSpace(seo) ? null : seo; changed = true; }
            var content = ReplaceAll(page.Content);
            if (content != page.Content) { page.Content = content; changed = true; }
            if (changed) count++;
        }

        foreach (var block in await db.HtmlBlocks.ToListAsync(ct))
        {
            var content = ReplaceAll(block.Content);
            if (content != block.Content)
            {
                block.Content = content;
                count++;
            }
        }

        var slidesSetting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == RemoteImageImport.SlidesKey, ct);
        if (slidesSetting is not null && !string.IsNullOrWhiteSpace(slidesSetting.Value))
        {
            try
            {
                var slides = JsonSerializer.Deserialize<List<ShopSlide>>(slidesSetting.Value) ?? [];
                var changed = false;
                var nextSlides = new List<ShopSlide>(slides.Count);
                foreach (var slide in slides)
                {
                    var image = ReplaceAll(slide.ImageUrl);
                    var alt = slide.Alt;
                    if (oldUrls.Contains(slide.ImageUrl) || !string.Equals(image, slide.ImageUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsUsefulAlt(alt))
                            alt = newAlt;
                        changed = true;
                    }

                    nextSlides.Add(new ShopSlide
                    {
                        ImageUrl = image,
                        LinkUrl = slide.LinkUrl,
                        Alt = alt
                    });
                    if (!string.Equals(image, slide.ImageUrl, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(alt, slide.Alt, StringComparison.Ordinal))
                        changed = true;
                }

                if (changed)
                {
                    slidesSetting.Value = JsonSerializer.Serialize(nextSlides);
                    count++;
                }
            }
            catch (JsonException)
            {
                // ignore invalid slide json
            }
        }

        var logoSetting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == "LogoUrl", ct);
        if (logoSetting is not null && !string.IsNullOrWhiteSpace(logoSetting.Value))
        {
            var nextLogo = ReplaceAll(logoSetting.Value);
            if (!string.Equals(nextLogo, logoSetting.Value, StringComparison.Ordinal))
            {
                logoSetting.Value = nextLogo;
                count++;
            }
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    private static Dictionary<string, string> ExpandUrlMap(Dictionary<string, string> urlMap)
    {
        var map = new Dictionary<string, string>(urlMap, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in urlMap.ToList())
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                continue;

            var oldFile = Path.GetFileName(pair.Key);
            var newFile = Path.GetFileName(pair.Value);
            if (string.IsNullOrWhiteSpace(oldFile) || string.IsNullOrWhiteSpace(newFile))
                continue;

            // Content often stores flat /uploads/{file} even when library has optimized paths.
            var flatOld = "/uploads/" + oldFile;
            var flatNew = "/uploads/" + newFile;
            if (!map.ContainsKey(flatOld))
                map[flatOld] = pair.Value.Contains("/uploads/optimized/", StringComparison.OrdinalIgnoreCase)
                    || pair.Value.Contains("/uploads/originals/", StringComparison.OrdinalIgnoreCase)
                    ? pair.Value
                    : flatNew;
        }

        return map;
    }

    private static HashSet<string> UrlSet(MediaAsset asset)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? url)
        {
            if (!string.IsNullOrWhiteSpace(url))
                set.Add(url);
        }

        Add(asset.OriginalUrl);
        Add(asset.ThumbUrl);
        Add(asset.MediumUrl);
        Add(asset.LargeUrl);
        var file = Path.GetFileName(asset.FileName);
        if (!string.IsNullOrWhiteSpace(file))
            Add("/uploads/" + file);
        return set;
    }

    private static bool UrlMatches(string? stored, HashSet<string> urls)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return false;
        if (urls.Contains(stored))
            return true;
        foreach (var url in urls)
        {
            if (stored.Contains(url, StringComparison.OrdinalIgnoreCase)
                || url.EndsWith(Path.GetFileName(stored), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsUsefulAlt(string? alt)
    {
        if (string.IsNullOrWhiteSpace(alt))
            return false;
        var value = alt.Trim();
        if (value.Length < 3)
            return false;
        // Corrupted UTF-8 often becomes '?' or U+FFFD — never use for SEO slug/alt.
        if (value.Contains('?') || value.Contains('\uFFFD'))
            return false;
        if (value.Contains("tối ưu lỗi", StringComparison.OrdinalIgnoreCase))
            return false;
        if (Regex.IsMatch(value, @"^(img|image|dsc|photo|picture|anh)[-_\s]?\d*$", RegexOptions.IgnoreCase))
            return false;
        return true;
    }

    private static string TitleFromSlug(string slug) =>
        CultureTitle(slug.Replace('-', ' '));

    private static string CultureTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Ảnh";
        var chars = value.Trim().ToCharArray();
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

    private static string BaseNameWithoutStamp(string fileNameWithoutExt)
    {
        var (baseName, _) = SplitBaseAndStamp(fileNameWithoutExt);
        return baseName;
    }

    private static (string Base, string Stamp) SplitBaseAndStamp(string fileNameWithoutExt)
    {
        var source = fileNameWithoutExt ?? string.Empty;
        var match = StampSuffix.Match(source);
        if (match.Success)
        {
            var baseName = source[..^match.Length].Trim('-');
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "anh";
            return (baseName, match.Groups[1].Value);
        }

        return (
            string.IsNullOrWhiteSpace(fileNameWithoutExt) ? "anh" : fileNameWithoutExt,
            DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
    }

    private static string ExtractStampOrDefault(string relativePath, string fallback)
    {
        var name = Path.GetFileNameWithoutExtension(relativePath);
        var match = StampSuffix.Match(name ?? string.Empty);
        return match.Success ? match.Groups[1].Value : fallback;
    }

    private static string AllocateUniqueBase(string desired, HashSet<string> used, string? currentBase)
    {
        var baseName = string.IsNullOrWhiteSpace(desired) ? "anh" : desired;
        if (!used.Contains(baseName) || string.Equals(baseName, currentBase, StringComparison.OrdinalIgnoreCase))
        {
            used.Add(baseName);
            return baseName;
        }

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName}-{i:00}";
            if (candidate.Length > 60)
                candidate = candidate[..60].Trim('-');
            if (!used.Contains(candidate) || string.Equals(candidate, currentBase, StringComparison.OrdinalIgnoreCase))
            {
                used.Add(candidate);
                return candidate;
            }
        }

        var fallback = $"{baseName}-{DateTime.UtcNow:HHmmss}";
        used.Add(fallback);
        return fallback;
    }
}
