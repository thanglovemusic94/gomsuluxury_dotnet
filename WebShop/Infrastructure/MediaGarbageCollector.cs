using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Periodically soft-deletes unused media (never hard-deletes files).
/// Skips assets referenced in content and protected settings (logo/slides).
/// </summary>
public sealed class MediaGarbageCollector(
    IServiceScopeFactory scopes,
    IOptions<MediaOptions> options,
    ILogger<MediaGarbageCollector> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var days = Math.Max(1, options.Value.GarbageCollectIntervalDays);
        logger.LogInformation(
            "MediaGarbageCollector registered (enabled={Enabled}, intervalDays={Days})",
            options.Value.GarbageCollectEnabled,
            days);

        // Wait a full interval before the first run to avoid surprising deletes on deploy.
        try
        {
            await Task.Delay(TimeSpan.FromDays(days), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Value.GarbageCollectEnabled)
                    await RunOnceAsync(stoppingToken);
                else
                    logger.LogDebug("MediaGarbageCollector skipped (disabled).");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MediaGarbageCollector failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromDays(Math.Max(1, options.Value.GarbageCollectIntervalDays)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var usage = scope.ServiceProvider.GetRequiredService<MediaUsageService>();
        var media = scope.ServiceProvider.GetRequiredService<MediaStorage>();

        await media.EnsureSchemaAsync(ct);

        var candidates = await db.MediaAssets
            .Where(item => item.IsImage)
            .ToListAsync(ct);

        await BackfillMissingHashesAsync(media, candidates, ct);

        var keepIds = await usage.GetReferencedOrProtectedAssetIdsAsync(candidates, ct);
        var orphaned = candidates.Where(item => !keepIds.Contains(item.Id)).ToList();

        var count = 0;
        foreach (var asset in orphaned)
        {
            SoftDelete.Mark(asset, "MediaGarbageCollector");
            asset.UpdatedAt = DateTime.UtcNow;
            count++;
        }

        await db.SaveChangesAsync(ct);
        if (count == 0)
            logger.LogInformation("MediaGarbageCollector: no unused images.");
        else
            logger.LogInformation("MediaGarbageCollector: soft-deleted {Count} unused images.", count);
        return count;
    }

    private static async Task BackfillMissingHashesAsync(
        MediaStorage media,
        List<MediaAsset> assets,
        CancellationToken ct)
    {
        foreach (var asset in assets)
        {
            if (!string.IsNullOrWhiteSpace(asset.ContentHash))
                continue;

            var abs = Path.Combine(media.RootPath, asset.OriginalPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(abs))
                continue;

            try
            {
                await using var stream = File.OpenRead(abs);
                asset.ContentHash = await MediaHash.Sha256HexAsync(stream, ct);
                asset.UpdatedAt = DateTime.UtcNow;
            }
            catch
            {
                // ignore unreadable originals
            }
        }
    }
}
