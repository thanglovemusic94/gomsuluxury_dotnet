using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shop;

public class AboutModel(AppDbContext db, ShopStore shop, MediaTrashFilter trash) : PageModel
{
    public string SiteName { get; private set; } = "Gốm Sứ Luxury";
    public string HeroImage { get; private set; } = string.Empty;
    public string StoryImage { get; private set; } = string.Empty;
    public string CraftImage { get; private set; } = string.Empty;
    public IReadOnlyList<string> Gallery { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var snap = await shop.GetAsync();
        SiteName = string.IsNullOrWhiteSpace(snap.SiteName) ? SiteName : snap.SiteName;

        var page = await db.CustomPages.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IsPublished && item.Slug == "gioi-thieu");

        ViewData["Title"] = page?.MetaTitle ?? "Giới thiệu";
        ViewData["Description"] = page?.MetaDescription
            ?? "Gốm Sứ Luxury – tự hào gốm Việt. Vẻ đẹp thuần Việt, đẳng cấp toàn cầu.";
        ViewData["OgType"] = "article";
        ViewData["ShopAssets"] = "detail";

        // Query filter MediaAssets đã loại IsDeleted; vẫn lọc Seo/Share nếu trỏ URL thùng rác.
        var media = await db.MediaAssets.AsNoTracking()
            .Where(item => item.IsImage)
            .OrderByDescending(item => item.Id)
            .Select(item => new MediaRow(item.Folder, item.FileName, item.LargeUrl, item.MediumUrl, item.OriginalUrl))
            .ToListAsync();

        HeroImage = SiteSocial.PickImage(
            Prefer(media,
                row => NameHas(row, "gioi-thieu"),
                row => FolderIs(row, "trang"),
                row => FolderIs(row, "slider") && NameHas(row, "bat-trang"),
                row => FolderIs(row, "slider") && NameHas(row, "gom")),
            await trash.LiveOrNullAsync(page?.SeoImage),
            await trash.LiveOrNullAsync(snap.ShareImageUrl));

        StoryImage = Prefer(media,
            row => FolderIs(row, "slider") && NameHas(row, "bat-trang"),
            row => FolderIs(row, "slider") && NameHas(row, "nhat-ma"),
            row => FolderIs(row, "trang"),
            row => FolderIs(row, "san-pham"));

        CraftImage = Prefer(media,
            row => FolderIs(row, "san-pham") && NameHas(row, "dsc"),
            row => FolderIs(row, "san-pham"),
            row => FolderIs(row, "slider"));

        if (Same(StoryImage, HeroImage))
            StoryImage = Prefer(media,
                row => FolderIs(row, "slider") && !Same(UrlOf(row), HeroImage),
                row => FolderIs(row, "san-pham") && !Same(UrlOf(row), HeroImage));

        if (Same(CraftImage, HeroImage) || Same(CraftImage, StoryImage))
            CraftImage = Prefer(media,
                row => FolderIs(row, "san-pham")
                       && !Same(UrlOf(row), HeroImage)
                       && !Same(UrlOf(row), StoryImage));

        Gallery = media
            .Where(row => FolderIs(row, "san-pham")
                          || FolderIs(row, "trang")
                          || (FolderIs(row, "slider") && (NameHas(row, "gom") || NameHas(row, "bat-trang") || NameHas(row, "nhat-ma"))))
            .Select(UrlOf)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        if (!string.IsNullOrWhiteSpace(HeroImage))
        {
            ViewData["OgImage"] = HeroImage;
            ViewData["LcpImage"] = HeroImage;
        }
    }

    private sealed record MediaRow(string Folder, string FileName, string? LargeUrl, string? MediumUrl, string? OriginalUrl);

    private static string UrlOf(MediaRow? row)
    {
        if (row is null)
            return string.Empty;
        var raw = row.LargeUrl ?? row.MediumUrl ?? row.OriginalUrl;
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : MediaUrls.For(raw, MediaSize.Large);
    }

    private static bool FolderIs(MediaRow row, string folder) =>
        string.Equals(row.Folder, folder, StringComparison.OrdinalIgnoreCase);

    private static bool NameHas(MediaRow row, string token) =>
        row.FileName.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool Same(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a)
        && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Prefer(IReadOnlyList<MediaRow> media, params Func<MediaRow, bool>[] predicates)
    {
        foreach (var predicate in predicates)
        {
            foreach (var row in media)
            {
                if (!predicate(row))
                    continue;
                var url = UrlOf(row);
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
        }

        return string.Empty;
    }
}
