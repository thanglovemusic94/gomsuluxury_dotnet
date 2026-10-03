using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// One-shot: regenerate optimized variants at current MediaOptions (WebpQuality 50)
/// and keep product/post/slide URLs aligned.
/// </summary>
public static class MediaOptimizeBackfill
{
    private const string MarkerKey = "Media.Optimize.V7";

    public static async Task EnsureAsync(AppDbContext db, MediaStorage media, ILogger logger)
    {
        if (await db.SystemSettings.AnyAsync(item => item.Key == MarkerKey && item.Value == "1"))
            return;

        logger.LogInformation("MediaOptimizeBackfill V7: regenerating optimized images (q50)...");
        var (ok, error) = await media.RegenerateAllAsync();
        if (!string.IsNullOrWhiteSpace(error))
            logger.LogWarning("MediaOptimizeBackfill V7 notes: {Error}", error);
        else
            logger.LogInformation("MediaOptimizeBackfill V7: done (ok={Ok})", ok);

        var legacy = await db.SystemSettings
            .Where(item => item.Key.StartsWith("Media.Optimize."))
            .ToListAsync();
        foreach (var row in legacy.Where(item => item.Key != MarkerKey))
            db.SystemSettings.Remove(row);

        var marker = legacy.FirstOrDefault(item => item.Key == MarkerKey);
        if (marker is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = MarkerKey,
                Value = "1",
                Description = "Đã regenerate ảnh tối ưu V7 (WebpQuality 50, giữ URL + remap)"
            });
        }
        else
        {
            marker.Value = "1";
            marker.Description = "Đã regenerate ảnh tối ưu V7 (WebpQuality 50, giữ URL + remap)";
        }

        await db.SaveChangesAsync();
    }
}
