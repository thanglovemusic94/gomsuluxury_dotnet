using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Built-in + custom media folders (SystemSettings <c>Media.CustomFolders</c>).
/// Custom keys có thể lồng: <c>san-pham/chien-dich</c>.
/// </summary>
public sealed class MediaFolderService(AppDbContext db, IWebHostEnvironment env, MediaTrashFilter trash)
{
    public const string SettingsKey = "Media.CustomFolders";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<MediaFolderEntry>> ListAsync(CancellationToken ct = default)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in MediaFolders.Items)
            labels[item.Key] = item.Label;

        foreach (var custom in await LoadCustomAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(custom.Key) || MediaFolders.IsBuiltIn(custom.Key))
                continue;
            var key = MediaFolders.Normalize(custom.Key);
            if (key == MediaFolders.All)
                continue;
            labels[key] = string.IsNullOrWhiteSpace(custom.Label)
                ? MediaFolders.LeafLabel(key)
                : custom.Label.Trim();
            EnsureAncestorPlaceholders(labels, key);
        }

        // Orphan keys still present on assets
        var used = await db.MediaAssets.AsNoTracking()
            .Select(item => item.Folder)
            .Distinct()
            .ToListAsync(ct);
        foreach (var raw in used)
        {
            var key = MediaFolders.Normalize(raw);
            if (key == MediaFolders.All || labels.ContainsKey(key))
                continue;
            labels[key] = MediaFolders.LeafLabel(key);
            EnsureAncestorPlaceholders(labels, key);
        }

        return labels
            .Select(item => new MediaFolderEntry(
                item.Key,
                item.Value,
                MediaFolders.Depth(item.Key)))
            .OrderBy(item => MediaFolders.RootSortOrder(item.Key))
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Tạo thư mục con dưới <paramref name="parentFolder"/> (rỗng / Tất cả = gốc).
    /// </summary>
    public async Task<(bool Ok, string? Key, string? Error)> CreateAsync(
        string? name,
        string? parentFolder = null,
        CancellationToken ct = default)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (false, null, "Nhập tên thư mục.");

        var leaf = MediaFolders.ToSlug(name);
        if (string.IsNullOrWhiteSpace(leaf) || leaf is "all" or "tat-ca")
            return (false, null, "Tên thư mục không hợp lệ.");

        var parent = MediaFolders.Normalize(parentFolder);
        if (parent != MediaFolders.All
            && MediaFolders.PathSegments(parent).Length >= MediaFolders.MaxDepth)
            return (false, null, $"Chỉ lồng tối đa {MediaFolders.MaxDepth} cấp thư mục.");

        var key = parent == MediaFolders.All ? leaf : $"{parent}/{leaf}";
        key = MediaFolders.Normalize(key);
        if (key == MediaFolders.All)
            return (false, null, "Tên thư mục không hợp lệ.");
        if (key.Length > MediaFolders.MaxPathLength)
            return (false, null, "Đường dẫn thư mục quá dài.");
        if (MediaFolders.IsBuiltIn(key))
            return (false, null, "Tên trùng thư mục hệ thống.");

        var existing = await ListAsync(ct);
        if (existing.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)))
            return (false, null, "Thư mục đã tồn tại.");

        var customs = (await LoadCustomAsync(ct)).ToList();
        customs.Add(new CustomFolderDto(key, name));
        await SaveCustomAsync(customs, ct);
        EnsureDiskFolders(key);
        return (true, key, null);
    }

    /// <summary>Số file + thư mục con sẽ bị xóa cascade (để hiện xác nhận).</summary>
    public async Task<(int Files, int Subfolders)> GetDeleteImpactAsync(
        string? folder,
        CancellationToken ct = default)
    {
        var key = MediaFolders.Normalize(folder);
        if (key == MediaFolders.All || MediaFolders.IsBuiltIn(key))
            return (0, 0);

        var prefix = key + "/";
        var subfolders = (await ListAsync(ct))
            .Count(item => item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        var files = await db.MediaAssets
            .CountAsync(item => item.Folder == key || item.Folder.StartsWith(prefix), ct);
        return (files, subfolders);
    }

    /// <summary>
    /// Xóa cascade thư mục custom: soft-delete mọi file trong cây, gỡ mọi thư mục con + chính nó.
    /// Không xóa thư mục hệ thống (built-in).
    /// </summary>
    public async Task<(bool Ok, string? ParentKey, int Files, int Subfolders, string? Error)> DeleteAsync(
        string? folder,
        string? deletedBy = null,
        CancellationToken ct = default)
    {
        var key = MediaFolders.Normalize(folder);
        if (key == MediaFolders.All)
            return (false, null, 0, 0, "Không xóa được «Tất cả».");
        if (MediaFolders.IsBuiltIn(key))
            return (false, null, 0, 0, "Không xóa thư mục hệ thống.");

        var prefix = key + "/";
        var allFolders = await ListAsync(ct);
        var subfolderKeys = allFolders
            .Where(item => item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Key)
            .ToList();
        var subfolderCount = subfolderKeys.Count;

        // Soft-delete mọi file trong folder + cây con (chưa bị xóa)
        var assets = await db.MediaAssets
            .Where(item => item.Folder == key || item.Folder.StartsWith(prefix))
            .ToListAsync(ct);
        foreach (var asset in assets)
        {
            SoftDelete.Mark(asset, deletedBy);
            asset.UpdatedAt = DateTime.UtcNow;
        }

        if (assets.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            trash.Invalidate();
        }

        // Gỡ custom keys: chính folder + mọi con
        var customs = (await LoadCustomAsync(ct)).ToList();
        customs.RemoveAll(item =>
        {
            var k = MediaFolders.Normalize(item.Key);
            return string.Equals(k, key, StringComparison.OrdinalIgnoreCase)
                   || k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        });
        await SaveCustomAsync(customs, ct);

        // Thử gỡ thư mục disk trống (file soft-delete vẫn giữ trên đĩa)
        foreach (var child in subfolderKeys.OrderByDescending(k => k.Length))
            TryRemoveEmptyDiskFolders(child);
        TryRemoveEmptyDiskFolders(key);

        return (true, MediaFolders.ParentKey(key), assets.Count, subfolderCount, null);
    }

    /// <summary>Đổi tên lá thư mục custom (cập nhật key cây con + Folder của file).</summary>
    public async Task<(bool Ok, string? NewKey, string? Error)> RenameAsync(
        string? folder,
        string? newName,
        CancellationToken ct = default)
    {
        var key = MediaFolders.Normalize(folder);
        if (key == MediaFolders.All)
            return (false, null, "Không đổi tên «Tất cả».");
        if (MediaFolders.IsBuiltIn(key))
            return (false, null, "Không đổi tên thư mục hệ thống.");

        newName = (newName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newName))
            return (false, null, "Nhập tên thư mục.");

        var leaf = MediaFolders.ToSlug(newName);
        if (string.IsNullOrWhiteSpace(leaf) || leaf is "all" or "tat-ca")
            return (false, null, "Tên thư mục không hợp lệ.");

        var parent = MediaFolders.ParentKey(key);
        var newKey = parent == MediaFolders.All ? leaf : $"{parent}/{leaf}";
        newKey = MediaFolders.Normalize(newKey);
        if (newKey == MediaFolders.All || MediaFolders.IsBuiltIn(newKey))
            return (false, null, "Tên trùng thư mục hệ thống.");

        if (string.Equals(newKey, key, StringComparison.OrdinalIgnoreCase))
        {
            // Chỉ đổi nhãn hiển thị
            var customs = (await LoadCustomAsync(ct)).ToList();
            var idx = customs.FindIndex(item =>
                string.Equals(MediaFolders.Normalize(item.Key), key, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                customs[idx] = new CustomFolderDto(key, newName);
            else
                customs.Add(new CustomFolderDto(key, newName));
            await SaveCustomAsync(customs, ct);
            return (true, key, null);
        }

        if ((await ListAsync(ct)).Any(item =>
                string.Equals(item.Key, newKey, StringComparison.OrdinalIgnoreCase)))
            return (false, null, "Thư mục đích đã tồn tại.");

        var (ok, error) = await RemapTreeAsync(key, newKey, newName, ct);
        return ok ? (true, newKey, null) : (false, null, error);
    }

    /// <summary>Chuyển thư mục custom sang parent mới (kéo theo cây con + nhãn Folder trên file).</summary>
    public async Task<(bool Ok, string? NewKey, string? Error)> MoveAsync(
        string? folder,
        string? newParentFolder,
        CancellationToken ct = default)
    {
        var key = MediaFolders.Normalize(folder);
        if (key == MediaFolders.All)
            return (false, null, "Không di chuyển «Tất cả».");
        if (MediaFolders.IsBuiltIn(key))
            return (false, null, "Không di chuyển thư mục hệ thống.");

        var parent = MediaFolders.Normalize(newParentFolder);
        if (string.Equals(parent, key, StringComparison.OrdinalIgnoreCase)
            || parent.StartsWith(key + "/", StringComparison.OrdinalIgnoreCase))
            return (false, null, "Không thể chuyển vào chính nó hoặc thư mục con.");

        if (parent != MediaFolders.All && MediaFolders.IsBuiltIn(parent) == false)
        {
            var known = await ListAsync(ct);
            if (!known.Any(item => string.Equals(item.Key, parent, StringComparison.OrdinalIgnoreCase)))
                return (false, null, "Thư mục đích không tồn tại.");
        }

        var leaf = MediaFolders.LeafKey(key);
        var newKey = parent == MediaFolders.All ? leaf : $"{parent}/{leaf}";
        newKey = MediaFolders.Normalize(newKey);
        if (newKey == MediaFolders.All)
            return (false, null, "Đường dẫn không hợp lệ.");
        if (string.Equals(newKey, key, StringComparison.OrdinalIgnoreCase))
            return (true, key, null);

        // Độ sâu sau khi chuyển (kể cả nhánh sâu nhất)
        var oldSeg = MediaFolders.PathSegments(key).Length;
        var newSeg = MediaFolders.PathSegments(newKey).Length;
        var maxExtra = 0;
        foreach (var item in await ListAsync(ct))
        {
            if (!item.Key.StartsWith(key + "/", StringComparison.OrdinalIgnoreCase))
                continue;
            maxExtra = Math.Max(maxExtra, MediaFolders.PathSegments(item.Key).Length - oldSeg);
        }

        if (newSeg + maxExtra > MediaFolders.MaxDepth)
            return (false, null, $"Chỉ lồng tối đa {MediaFolders.MaxDepth} cấp thư mục.");

        if ((await ListAsync(ct)).Any(item =>
                string.Equals(item.Key, newKey, StringComparison.OrdinalIgnoreCase)))
            return (false, null, "Đã có thư mục cùng tên tại đích.");

        var (ok, error) = await RemapTreeAsync(key, newKey, newLabelForRoot: null, ct);
        return ok ? (true, newKey, null) : (false, null, error);
    }

    private async Task<(bool Ok, string? Error)> RemapTreeAsync(
        string oldKey,
        string newKey,
        string? newLabelForRoot,
        CancellationToken ct)
    {
        var oldPrefix = oldKey + "/";
        var newPrefix = newKey + "/";

        var assets = await db.MediaAssets.IgnoreQueryFilters()
            .Where(item => item.Folder == oldKey || item.Folder.StartsWith(oldPrefix))
            .ToListAsync(ct);
        foreach (var asset in assets)
        {
            var folder = asset.Folder ?? string.Empty;
            if (string.Equals(folder, oldKey, StringComparison.OrdinalIgnoreCase))
                asset.Folder = newKey;
            else if (folder.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                asset.Folder = newPrefix + folder[oldPrefix.Length..];
            asset.UpdatedAt = DateTime.UtcNow;
        }

        if (assets.Count > 0)
            await db.SaveChangesAsync(ct);

        var customs = (await LoadCustomAsync(ct)).ToList();
        var remapped = new List<CustomFolderDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in customs)
        {
            var k = MediaFolders.Normalize(item.Key);
            string nextKey;
            string nextLabel = item.Label;
            if (string.Equals(k, oldKey, StringComparison.OrdinalIgnoreCase))
            {
                nextKey = newKey;
                if (!string.IsNullOrWhiteSpace(newLabelForRoot))
                    nextLabel = newLabelForRoot.Trim();
            }
            else if (k.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
            {
                nextKey = MediaFolders.Normalize(newPrefix + k[oldPrefix.Length..]);
            }
            else
            {
                nextKey = k;
            }

            if (string.IsNullOrWhiteSpace(nextKey) || !seen.Add(nextKey))
                continue;
            remapped.Add(new CustomFolderDto(nextKey, nextLabel));
        }

        if (!seen.Contains(newKey))
            remapped.Add(new CustomFolderDto(newKey, newLabelForRoot ?? MediaFolders.LeafLabel(newKey)));

        await SaveCustomAsync(remapped, ct);
        EnsureDiskFolders(newKey);
        return (true, null);
    }

    private static void EnsureAncestorPlaceholders(Dictionary<string, string> labels, string key)
    {
        var parent = MediaFolders.ParentKey(key);
        while (parent != MediaFolders.All)
        {
            if (!labels.ContainsKey(parent))
                labels[parent] = MediaFolders.LeafLabel(parent);
            parent = MediaFolders.ParentKey(parent);
        }
    }

    private async Task<List<CustomFolderDto>> LoadCustomAsync(CancellationToken ct)
    {
        var row = await db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == SettingsKey, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<CustomFolderDto>>(row.Value, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveCustomAsync(List<CustomFolderDto> items, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(items, JsonOpts);
        var row = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == SettingsKey, ct);
        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = SettingsKey,
                Value = json,
                Description = "Thư mục media tùy chỉnh (JSON)"
            });
        }
        else
        {
            row.Value = json;
        }

        await db.SaveChangesAsync(ct);
    }

    private void EnsureDiskFolders(string key)
    {
        var segments = MediaFolders.PathSegments(key);
        if (segments.Length == 0)
            return;

        var root = Path.Combine(env.WebRootPath, "uploads");
        CreateNested(Path.Combine(root, "originals"), segments);
        CreateNested(Path.Combine(root, "optimized", "icon"), segments);
        CreateNested(Path.Combine(root, "optimized", "thumb"), segments);
        CreateNested(Path.Combine(root, "optimized", "medium"), segments);
        CreateNested(Path.Combine(root, "optimized", "large"), segments);
    }

    private static void CreateNested(string baseDir, string[] segments)
    {
        var path = baseDir;
        foreach (var seg in segments)
        {
            path = Path.Combine(path, seg);
            Directory.CreateDirectory(path);
        }
    }

    private void TryRemoveEmptyDiskFolders(string key)
    {
        var segments = MediaFolders.PathSegments(key);
        if (segments.Length == 0)
            return;

        var root = Path.Combine(env.WebRootPath, "uploads");
        TryRemoveNested(Path.Combine(root, "originals"), segments);
        TryRemoveNested(Path.Combine(root, "optimized", "icon"), segments);
        TryRemoveNested(Path.Combine(root, "optimized", "thumb"), segments);
        TryRemoveNested(Path.Combine(root, "optimized", "medium"), segments);
        TryRemoveNested(Path.Combine(root, "optimized", "large"), segments);
    }

    private static void TryRemoveNested(string baseDir, string[] segments)
    {
        var path = Path.Combine(new[] { baseDir }.Concat(segments).ToArray());
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
        catch
        {
            // ignore locked / non-empty
        }
    }

    private sealed record CustomFolderDto(string Key, string Label);

    /// <summary>Con trực tiếp của folder đang mở (Tất cả → các thư mục gốc).</summary>
    public static IReadOnlyList<MediaFolderEntry> ImmediateChildren(
        IReadOnlyList<MediaFolderEntry> all,
        string? current)
    {
        var parent = MediaFolders.Normalize(current);
        if (parent == MediaFolders.All)
            return all.Where(item => item.Depth == 0).ToList();

        return all
            .Where(item => string.Equals(
                MediaFolders.ParentKey(item.Key),
                parent,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
