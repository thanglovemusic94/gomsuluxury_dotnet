using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.DependencyInjection;
using SixLabors.ImageSharp.Web.Middleware;
using SixLabors.ImageSharp.Web.Processors;
using SixLabors.ImageSharp.Web.Providers;

namespace WebShop.Infrastructure;

/// <summary>
/// Hybrid on-the-fly pipeline: only /uploads/originals/* accepts resize/format commands.
/// Shop pages keep preset optimized URLs for stable SEO.
/// </summary>
public static class MediaImageSharpSetup
{
    public const string CacheFolderName = "is-cache";
    public const int MaxWidth = 1600;
    public const int MaxHeight = 1600;
    public const int MinDimension = 16;
    public const int DefaultQuality = 70;

    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "webp"
    };

    private static readonly HashSet<string> AllowedResizeModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "max", "crop", "pad", "boxpad", "min"
    };

    public static IServiceCollection AddShopImageSharp(this IServiceCollection services)
    {
        services.AddImageSharp(options =>
            {
                options.BrowserMaxAge = TimeSpan.FromDays(30);
                options.CacheMaxAge = TimeSpan.FromDays(365);
                options.CacheHashLength = 12;
                options.OnParseCommandsAsync = ParseCommandsAsync;
            })
            .Configure<PhysicalFileSystemCacheOptions>(cache =>
            {
                cache.CacheFolder = CacheFolderName;
                cache.CacheFolderDepth = 8;
            })
            .Configure<PhysicalFileSystemProviderOptions>(provider =>
            {
                // Only process when commands remain after allowlist (originals only).
                provider.ProcessingBehavior = ProcessingBehavior.CommandOnly;
            });

        return services;
    }

    public static IApplicationBuilder UseShopImageSharp(this IApplicationBuilder app)
    {
        var webRoot = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        Directory.CreateDirectory(Path.Combine(webRoot, CacheFolderName));
        return app.UseImageSharp();
    }

    private static Task ParseCommandsAsync(ImageCommandContext context)
    {
        var path = context.Context.Request.Path.Value ?? string.Empty;

        // Do not re-process already-optimized variants; keep SEO URLs as static files.
        if (!path.StartsWith("/uploads/originals/", StringComparison.OrdinalIgnoreCase))
        {
            context.Commands.Clear();
            return Task.CompletedTask;
        }

        if (context.Commands.Count == 0)
            return Task.CompletedTask;

        // Strip unsupported / dangerous commands.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ResizeWebProcessor.Width,
            ResizeWebProcessor.Height,
            ResizeWebProcessor.Mode,
            FormatWebProcessor.Format,
            QualityWebProcessor.Quality,
            AutoOrientWebProcessor.AutoOrient
        };

        foreach (var key in context.Commands.Keys.ToList())
        {
            if (!allowed.Contains(key))
                context.Commands.Remove(key);
        }

        ClampIntCommand(context.Commands, ResizeWebProcessor.Width, MinDimension, MaxWidth);
        ClampIntCommand(context.Commands, ResizeWebProcessor.Height, MinDimension, MaxHeight);

        if (context.Commands.TryGetValue(ResizeWebProcessor.Mode, out var mode)
            && !AllowedResizeModes.Contains(mode))
            context.Commands[ResizeWebProcessor.Mode] = "max";

        if (context.Commands.TryGetValue(FormatWebProcessor.Format, out var format))
        {
            format = format.Trim().TrimStart('.');
            if (string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase))
                format = "jpg";
            if (!AllowedFormats.Contains(format))
                context.Commands.Remove(FormatWebProcessor.Format);
            else
                context.Commands[FormatWebProcessor.Format] = format;
        }

        ClampIntCommand(context.Commands, QualityWebProcessor.Quality, 30, 90);

        // Ensure we always have a sane default mode when resizing.
        if ((context.Commands.Contains(ResizeWebProcessor.Width)
             || context.Commands.Contains(ResizeWebProcessor.Height))
            && !context.Commands.Contains(ResizeWebProcessor.Mode))
        {
            context.Commands[ResizeWebProcessor.Mode] = "max";
        }

        return Task.CompletedTask;
    }

    private static void ClampIntCommand(CommandCollection commands, string key, int min, int max)
    {
        if (!commands.TryGetValue(key, out var raw))
            return;
        if (!int.TryParse(raw, out var value))
        {
            commands.Remove(key);
            return;
        }

        commands[key] = Math.Clamp(value, min, max).ToString();
    }
}
