using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>
/// Ghi content HTML trang /gioi-thieu (CustomPages) + kéo ảnh sắc từ nguồn.
/// Bump <see cref="VersionKey"/> khi cần viết lại.
/// </summary>
public static class AboutPageSeed
{
    public const string VersionKey = "About.PageContent.v3";
    public const string Slug = "gioi-thieu";

    private static readonly string[] RemoteImages =
    [
        "https://gomsuluxury.vn/wp-content/uploads/2025/08/DSC01098-scaled.jpg",
        "https://gomsuluxury.vn/wp-content/uploads/2025/08/z6374355635990_0651a9662968dfd45c44d72db7a6bd24.jpg",
        "https://gomsuluxury.vn/wp-content/uploads/2024/03/z5575587068622_ec7ce2f65674a92f5942376ab3640661.jpg",
        "https://gomsuluxury.vn/wp-content/uploads/2024/05/z5452864969918_61a4752a13a8c8293691b2f91e6600c1.jpg",
        "https://gomsuluxury.vn/wp-content/uploads/2025/12/Nhat-ma-thien-ha-thuy-15.png",
        "https://gomsuluxury.vn/wp-content/uploads/2024/02/wweb.jpg"
    ];

    public static async Task EnsureAsync(
        AppDbContext db,
        MediaStorage media,
        IHttpClientFactory httpFactory,
        ILogger? logger = null)
    {
        if (await db.SystemSettings.AnyAsync(item => item.Key == VersionKey && item.Value == "1"))
            return;

        await media.EnsureSchemaAsync();
        using var http = httpFactory.CreateClient("media-import");

        var urls = new List<string>();
        foreach (var remote in RemoteImages)
        {
            try
            {
                var prefer = remote.Contains("DSC01098", StringComparison.OrdinalIgnoreCase)
                             || remote.Contains("Nhat-ma", StringComparison.OrdinalIgnoreCase)
                    ? MediaSize.Original
                    : MediaSize.Large;
                var (ok, url, error, _) = await media.ImportFromUrlAsync(
                    http, remote, "Giới thiệu Gốm Sứ Luxury", prefer, MediaFolders.Pages);
                if (ok && !string.IsNullOrWhiteSpace(url))
                {
                    // Prefer Large path for display sharpness without huge PNG in HTML.
                    var display = MediaUrls.For(url, MediaSize.Large);
                    urls.Add(string.IsNullOrWhiteSpace(display) ? url : display);
                    logger?.LogInformation("About image OK: {Remote} → {Local}", remote, display ?? url);
                }
                else
                    logger?.LogWarning("About image fail: {Remote} — {Error}", remote, error);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "About image error: {Remote}", remote);
            }
        }

        // Fallback to sharp local slider assets if import missed some.
        if (urls.Count < 4)
        {
            var extras = await db.MediaAssets.AsNoTracking()
                .Where(item => item.IsImage && (item.Folder == MediaFolders.Slider || item.Folder == MediaFolders.Pages))
                .OrderByDescending(item => item.OriginalBytes)
                .Select(item => item.LargeUrl ?? item.OriginalUrl)
                .Take(8)
                .ToListAsync();
            foreach (var u in extras)
            {
                if (string.IsNullOrWhiteSpace(u)) continue;
                var display = MediaUrls.For(u, MediaSize.Large);
                if (!string.IsNullOrWhiteSpace(display)
                    && !urls.Contains(display, StringComparer.OrdinalIgnoreCase))
                    urls.Add(display);
                if (urls.Count >= 6) break;
            }
        }

        while (urls.Count < 6)
            urls.Add(urls.Count > 0 ? urls[^1] : "/favicon.svg");

        var hero = urls[0];
        var story = urls[1];
        var craft = urls[2];
        var g1 = urls[3];
        var g2 = urls[4];
        var g3 = urls[5];

        var html = BuildHtml(hero, story, craft, g1, g2, g3);

        var page = await db.CustomPages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Slug == Slug);
        if (page is null)
        {
            page = new CustomPage
            {
                Title = "Giới thiệu",
                Slug = Slug,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            db.CustomPages.Add(page);
        }

        page.Title = "Giới thiệu";
        page.Content = html;
        page.IsPublished = true;
        page.IsDeleted = false;
        page.DeletedAt = null;
        page.DeletedBy = null;
        page.MetaTitle = "Giới thiệu | Gốm Sứ Luxury – Tự hào gốm Việt";
        page.MetaDescription =
            "Gốm Sứ Luxury – vẻ đẹp thuần Việt, đẳng cấp toàn cầu. Tinh hoa thủ công Bát Tràng, sứ mệnh nâng tầm gốm Việt.";
        page.SeoImage = hero;
        page.SeoFocusKeyword = "gốm sứ luxury";
        page.SeoScore = 80;

        db.SystemSettings.Add(new SystemSetting
        {
            Key = VersionKey,
            Value = "1",
            Description = $"Đã ghi content /{Slug} + ảnh Large ({urls.Count} URL) lúc {DateTime.UtcNow:u}"
        });

        await db.SaveChangesAsync();
        logger?.LogInformation("About page content seeded ({Count} images).", urls.Count);
    }

    private static string BuildHtml(
        string hero, string story, string craft, string g1, string g2, string g3) =>
        $$"""
        <section class="shop-about-hero">
        	<div class="container shop-about-hero__intro">
        		<p class="shop-about-hero__eyebrow">Giới thiệu</p>
        		<h1 class="shop-about-hero__title">Gốm Sứ Luxury – Tự hào gốm Việt</h1>
        		<span class="shop-about-hero__rule" aria-hidden="true"></span>
        		<p class="shop-about-hero__lead">Vẻ đẹp thuần Việt, đẳng cấp toàn cầu</p>
        	</div>
        	<figure class="shop-about-hero__media">
        		<img src="{{hero}}" alt="Tinh hoa gốm Bát Tràng – Gốm Sứ Luxury" width="1600" height="900" fetchpriority="high" decoding="async" />
        	</figure>
        </section>

        <section class="shop-about-split">
        	<div class="container shop-about-split__grid">
        		<div class="shop-about-split__copy">
        			<h2>Câu chuyện thương hiệu</h2>
        			<p>Tại <strong>Gốm Sứ Luxury</strong>, mỗi món đồ là bản giao hưởng của đất, nước, lửa và đôi bàn tay nghệ nhân Việt. Chúng tôi không chạy theo sản xuất hàng loạt – mà chọn cách làm chậm, kỹ, để giữ hồn cốt của nghề.</p>
        			<p>Lấy cảm hứng từ tinh hoa gốm cổ truyền, kết hợp tư duy thiết kế đương đại, chúng tôi kiến tạo tuyệt phẩm cao cấp: vừa trang trọng trên bàn trà, vừa là điểm nhấn thẩm mỹ trong không gian sống.</p>
        		</div>
        		<figure class="shop-about-split__media">
        			<img src="{{story}}" alt="Không gian gốm sứ cao cấp" width="1200" height="1200" loading="lazy" decoding="async" />
        		</figure>
        	</div>
        </section>

        <section class="shop-about-pillars" aria-label="Sứ mệnh và tầm nhìn">
        	<div class="container">
        		<h2 class="shop-about-section-title">Sứ mệnh &amp; Tầm nhìn</h2>
        		<div class="shop-about-pillars__grid">
        			<article class="shop-about-card">
        				<span class="shop-about-card__icon" aria-hidden="true"><i class="fa fa-hand-paper-o"></i></span>
        				<h3>Sứ mệnh</h3>
        				<p>Gìn giữ và nâng tầm giá trị gốm Việt. Mỗi sản phẩm không chỉ để dùng hay trang trí – mà còn là <strong>niềm tự hào văn hoá Việt</strong>.</p>
        			</article>
        			<article class="shop-about-card">
        				<span class="shop-about-card__icon" aria-hidden="true"><i class="fa fa-eye"></i></span>
        				<h3>Tầm nhìn</h3>
        				<p>Trở thành biểu tượng gốm sứ Việt trong phân khúc cao cấp – nơi nghệ thuật và phong cách sống tinh tế gặp nhau.</p>
        			</article>
        			<article class="shop-about-card">
        				<span class="shop-about-card__icon" aria-hidden="true"><i class="fa fa-shield"></i></span>
        				<h3>Cam kết</h3>
        				<p>Tinh thần thủ công Bát Tràng, men và cốt gốm hoàn thiện kỹ – kiểm tra từng sản phẩm trước khi đến tay bạn.</p>
        			</article>
        		</div>
        	</div>
        </section>

        <section class="shop-about-split shop-about-split--reverse">
        	<div class="container shop-about-split__grid">
        		<figure class="shop-about-split__media">
        			<img src="{{craft}}" alt="Tác phẩm gốm thủ công Bát Tràng" width="1200" height="1200" loading="lazy" decoding="async" />
        		</figure>
        		<div class="shop-about-split__copy">
        			<h2>Người kể chuyện bằng gốm</h2>
        			<p><strong>Gốm Sứ Luxury</strong> không chỉ bán sản phẩm – chúng tôi kể chuyện bằng đất và lửa. Mỗi món đồ kết tinh lịch sử, nghệ thuật và công sức nghệ nhân, mang sự sang trọng cùng niềm tự hào vào ngôi nhà bạn.</p>
        			<p>Từ bộ ấm trà đến bình hoa, khay trà hay tượng trang trí: tất cả đều được tuyển chọn để đồng điệu với không gian sống tinh tế.</p>
        		</div>
        	</div>
        </section>

        <section class="shop-about-values" aria-label="Giá trị cốt lõi">
        	<div class="container">
        		<h2 class="shop-about-section-title">Giá trị cốt lõi</h2>
        		<ol class="shop-about-values__list">
        			<li>
        				<span class="shop-about-values__num" aria-hidden="true">01</span>
        				<div>
        					<h3>Tinh hoa thủ công</h3>
        					<p>Chế tác thủ công bởi nghệ nhân Bát Tràng nhiều năm kinh nghiệm – từng đường nét đều có chủ ý.</p>
        				</div>
        			</li>
        			<li>
        				<span class="shop-about-values__num" aria-hidden="true">02</span>
        				<div>
        					<h3>Bản sắc Việt</h3>
        					<p>Họa tiết và hình khối mang dấu ấn văn hoá từ dân gian đến cung đình, dễ nhận diện mà không cũ kỹ.</p>
        				</div>
        			</li>
        			<li>
        				<span class="shop-about-values__num" aria-hidden="true">03</span>
        				<div>
        					<h3>Tinh thần đổi mới</h3>
        					<p>Thiết kế độc bản trong khuôn khổ truyền thống, bắt kịp không gian sống hiện đại.</p>
        				</div>
        			</li>
        		</ol>
        	</div>
        </section>

        <section class="shop-about-stats" aria-label="Con số ấn tượng">
        	<div class="container shop-about-stats__grid">
        		<div class="shop-about-stat"><strong>15+</strong><span>Năm đồng hành nghề gốm</span></div>
        		<div class="shop-about-stat"><strong>50+</strong><span>Nghệ nhân Bát Tràng</span></div>
        		<div class="shop-about-stat"><strong>10.000+</strong><span>Khách hàng tin chọn</span></div>
        		<div class="shop-about-stat"><strong>100%</strong><span>Tuyển chọn thủ công</span></div>
        	</div>
        </section>

        <section class="shop-about-gallery" aria-labelledby="about-gallery-heading">
        	<div class="container">
        		<h2 id="about-gallery-heading" class="shop-about-section-title">Tinh hoa qua hình ảnh</h2>
        		<div class="shop-about-gallery__grid">
        			<figure class="shop-about-gallery__item shop-about-gallery__item--wide">
        				<img src="{{g1}}" alt="Bộ sưu tập gốm sứ Luxury" width="1600" height="800" loading="lazy" decoding="async" />
        			</figure>
        			<figure class="shop-about-gallery__item">
        				<img src="{{g2}}" alt="Chi tiết men và họa tiết" width="900" height="900" loading="lazy" decoding="async" />
        			</figure>
        			<figure class="shop-about-gallery__item">
        				<img src="{{g3}}" alt="Gốm Việt trong không gian sống" width="900" height="900" loading="lazy" decoding="async" />
        			</figure>
        		</div>
        	</div>
        </section>

        <section class="shop-about-quote">
        	<div class="container shop-about-quote__inner">
        		<blockquote>
        			<p>“Gốm Việt – niềm tự hào trong từng sản phẩm. Chúng tôi không chỉ làm gốm, chúng tôi gìn giữ hồn đất Việt.”</p>
        			<footer>— Đội ngũ Gốm Sứ Luxury</footer>
        		</blockquote>
        		<a class="btn btn-primary shop-about-quote__cta" href="/san-pham">Khám phá bộ sưu tập</a>
        	</div>
        </section>
        """;
}
