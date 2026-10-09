using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Settings;

public class IndexModel(AppDbContext db, SiteSeoService siteSeo, MediaSettingsService mediaSettings, MediaStorage media) : PageModel
{
    private static readonly HashSet<string> ManagedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        SiteSeoService.AllowIndexingKey,
        "Email",
        "OpeningHours",
        "OrgType",
        "YoutubeUrl",
        "SiteName",
        "Hotline",
        "Address",
        "LogoUrl",
        "FacebookUrl",
        "TikTokUrl",
        "ZaloUrl",
        "GeoCoordinates",
        "GeoLatitude",
        "GeoLongitude"
    };

    public IList<SystemSetting> Items { get; private set; } = [];

    [BindProperty]
    public bool AllowIndexing { get; set; }

    [BindProperty]
    public OrgInput Org { get; set; } = new();

    [BindProperty]
    public MediaInput Media { get; set; } = new();

    [BindProperty]
    public bool RegenerateMedia { get; set; }

    public async Task OnGetAsync()
    {
        AllowIndexing = await siteSeo.GetAllowIndexingAsync();
        var settings = await db.SystemSettings.AsNoTracking().ToListAsync();
        string V(string key) => settings.FirstOrDefault(item => item.Key == key)?.Value?.Trim() ?? string.Empty;

        var geo = SchemaOrg.NormalizePair(V("GeoCoordinates"));
        if (string.IsNullOrEmpty(geo))
            geo = SchemaOrg.NormalizePair($"{V("GeoLatitude")}, {V("GeoLongitude")}");

        Org = new OrgInput
        {
            SiteName = V("SiteName"),
            OrgType = string.IsNullOrWhiteSpace(V("OrgType")) ? "Store" : V("OrgType"),
            Email = V("Email"),
            Hotline = V("Hotline"),
            Address = V("Address"),
            OpeningHours = V("OpeningHours"),
            LogoUrl = V("LogoUrl"),
            FacebookUrl = V("FacebookUrl"),
            TikTokUrl = V("TikTokUrl"),
            ZaloUrl = V("ZaloUrl"),
            YoutubeUrl = V("YoutubeUrl"),
            GeoCoordinates = geo
        };

        var tune = mediaSettings.Current;
        Media = new MediaInput
        {
            IconWidth = tune.IconWidth,
            ThumbWidth = tune.ThumbWidth,
            MediumWidth = tune.MediumWidth,
            LargeWidth = tune.LargeWidth,
            WebpQuality = tune.WebpQuality,
            OtfQuality = tune.OtfQuality
        };

        Items = settings
            .Where(setting => !ManagedKeys.Contains(setting.Key) && !MediaSettingsService.ManagedKeys.Contains(setting.Key))
            .OrderBy(setting => setting.Key)
            .ToList();
    }

    public async Task<IActionResult> OnPostMediaAsync()
    {
        var (ok, error) = await mediaSettings.SaveAsync(new MediaTune(
            Media.IconWidth,
            Media.ThumbWidth,
            Media.MediumWidth,
            Media.LargeWidth,
            Media.WebpQuality,
            Media.OtfQuality));

        if (!ok)
        {
            TempData["Error"] = error ?? "Không lưu được cấu hình ảnh.";
            return RedirectToPage();
        }

        if (RegenerateMedia)
        {
            var (regenOk, regenError) = await media.RegenerateAllAsync();
            TempData[regenOk ? "Message" : "Error"] = regenOk
                ? "Đã lưu cấu hình ảnh và tạo lại toàn bộ bản tối ưu."
                : $"Đã lưu cấu hình ảnh. Tạo lại tối ưu: {regenError}";
        }
        else
        {
            TempData["Message"] =
                "Đã lưu cấu hình ảnh (áp dụng ngay cho upload mới / OTF). Ảnh cũ: bật “Tạo lại tối ưu” hoặc vào Media → Tạo lại tất cả tối ưu.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSeoAsync()
    {
        await siteSeo.SetAllowIndexingAsync(AllowIndexing);
        TempData["Message"] = AllowIndexing
            ? "Đã bật index cho công cụ tìm kiếm (Google)."
            : "Đã tắt index — Google sẽ không được khuyến khích quét site.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostOrgAsync()
    {
        await UpsertAsync("SiteName", Org.SiteName, "Tên cửa hàng / thương hiệu");
        await UpsertAsync("OrgType", string.IsNullOrWhiteSpace(Org.OrgType) ? "Store" : Org.OrgType.Trim(), "Schema @type Organization/Store/LocalBusiness");
        await UpsertAsync("Email", Org.Email, "Email liên hệ (Schema)");
        await UpsertAsync("Hotline", Org.Hotline, "Hotline");
        await UpsertAsync("Address", Org.Address, "Địa chỉ");
        await UpsertAsync("OpeningHours", Org.OpeningHours, "Giờ mở cửa Schema");
        await UpsertAsync("LogoUrl", Org.LogoUrl, "URL logo");
        await UpsertAsync("FacebookUrl", Org.FacebookUrl, "Facebook");
        await UpsertAsync("TikTokUrl", Org.TikTokUrl, "TikTok");
        await UpsertAsync("ZaloUrl", Org.ZaloUrl, "Zalo");
        await UpsertAsync("YoutubeUrl", Org.YoutubeUrl, "YouTube");

        var geo = SchemaOrg.NormalizePair(Org.GeoCoordinates);
        await UpsertAsync("GeoCoordinates", geo, "Tọa độ Google Maps (lat, lng) Schema");
        await RemoveLegacyGeoKeysAsync();

        await db.SaveChangesAsync();
        TempData["Message"] = string.IsNullOrEmpty(geo) && !string.IsNullOrWhiteSpace(Org.GeoCoordinates)
            ? "Đã lưu doanh nghiệp. Tọa độ không hợp lệ — vui lòng dán dạng 21.028511, 105.782302."
            : "Đã lưu thông tin doanh nghiệp / Schema Organization.";
        return RedirectToPage();
    }

    private async Task UpsertAsync(string key, string? value, string description)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == key);
        if (setting is null)
        {
            setting = new SystemSetting { Key = key, Description = description };
            db.SystemSettings.Add(setting);
        }

        setting.Value = TextHelper.Clean(value) ?? string.Empty;
        setting.Description ??= description;
    }

    private async Task RemoveLegacyGeoKeysAsync()
    {
        var legacy = await db.SystemSettings
            .Where(item => item.Key == "GeoLatitude" || item.Key == "GeoLongitude")
            .ToListAsync();
        if (legacy.Count > 0)
            db.SystemSettings.RemoveRange(legacy);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var setting = await db.SystemSettings.FindAsync(id);
        if (setting is null)
            return NotFound();

        if (ManagedKeys.Contains(setting.Key) || MediaSettingsService.ManagedKeys.Contains(setting.Key))
        {
            TempData["Error"] = "Khóa này chỉnh ở form phía trên, không xóa tại bảng.";
            return RedirectToPage();
        }

        db.SystemSettings.Remove(setting);
        var error = await DbSave.TryDeleteAsync(db);
        TempData[error is null ? "Message" : "Error"] = error ?? "Đã xóa cấu hình.";
        return RedirectToPage();
    }

    public class OrgInput
    {
        public string SiteName { get; set; } = string.Empty;
        public string OrgType { get; set; } = "Store";
        public string Email { get; set; } = string.Empty;
        public string Hotline { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string OpeningHours { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string FacebookUrl { get; set; } = string.Empty;
        public string TikTokUrl { get; set; } = string.Empty;
        public string ZaloUrl { get; set; } = string.Empty;
        public string YoutubeUrl { get; set; } = string.Empty;
        public string GeoCoordinates { get; set; } = string.Empty;
    }

    public class MediaInput
    {
        public int IconWidth { get; set; } = 96;
        public int ThumbWidth { get; set; } = 400;
        public int MediumWidth { get; set; } = 800;
        public int LargeWidth { get; set; } = 1200;
        public int WebpQuality { get; set; } = 85;
        public int OtfQuality { get; set; } = 85;
    }
}
