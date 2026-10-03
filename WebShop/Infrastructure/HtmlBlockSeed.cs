using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public static class HtmlBlockSeed
{
    private const string ProductTrustKey = "product-trust";
    private const string ProductTrustShortcode = "{{block:product-trust}}";
    private const string ScopedMarker = "HtmlBlock.ProductTrustScoped";

    public static async Task EnsureAsync(AppDbContext db)
    {
        await EnsureTableAsync(db);

        await ForceUpsertAsync(db, "home-promo", "Promo trang chủ",
            """
            <style>
            .shop-html-block{margin:20px 0;color:#3a2a24;line-height:1.6}
            .shop-html-block--promo{padding:16px 18px;background:linear-gradient(135deg,rgba(156,47,36,.08),rgba(201,162,74,.12));border-left:3px solid #6d1717}
            .shop-html-block--promo p{margin:0}
            </style>
            <div class="shop-html-block shop-html-block--promo">
            	<p><strong>Ưu đãi tuần này:</strong> Miễn phí giao hàng nội thành cho đơn từ 1.000.000đ. Liên hệ hotline để được tư vấn bộ ấm chén phù hợp.</p>
            </div>
            """,
            "Hiển thị dưới sản phẩm nổi bật trên trang chủ.");

        await ForceUpsertAsync(db, "footer-note", "Ghi chú footer",
            """
            <style>
            .shop-html-block--foot{margin:12px 0 0;font-size:13px;line-height:1.55;color:#fff6ea}
            .shop-html-block--foot p{margin:0;color:inherit}
            .shop-html-block--foot a{color:#f0d78a}
            </style>
            <p class="shop-html-block shop-html-block--foot">Cam kết gốm sứ chính hãng · Đóng gói cẩn thận · Hỗ trợ đổi trả theo chính sách.</p>
            """,
            "Hiển thị cuối phần thân footer.");

        await ForceUpsertAsync(db, ProductTrustKey, "Cam kết sản phẩm",
            """
            <style>
            .shop-trust{margin:28px 0 8px;padding:28px 24px;border:1px solid rgba(109,23,23,.12);background:radial-gradient(1200px 280px at 10% -20%,rgba(201,162,74,.16),transparent 60%),linear-gradient(180deg,#fffdf8,#faf4ec);color:#3a2a24}
            .shop-trust__eyebrow{display:inline-block;margin-bottom:8px;font-size:11px;font-weight:600;letter-spacing:.14em;text-transform:uppercase;color:#9c2f24}
            .shop-trust__title{margin:0 0 8px;font-size:24px;font-weight:600;line-height:1.25;color:#5a1616}
            .shop-trust__lead{margin:0 0 22px;max-width:54ch;font-size:15px;line-height:1.65;color:rgba(58,42,36,.82)}
            .shop-trust__grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px}
            .shop-trust__card{padding:16px 14px;background:rgba(255,255,255,.72);border:1px solid rgba(109,23,23,.08);min-height:100%}
            .shop-trust__icon{display:inline-flex;align-items:center;justify-content:center;width:36px;height:36px;margin-bottom:10px;border-radius:50%;background:rgba(156,47,36,.1);color:#9c2f24;font-size:15px}
            .shop-trust__card h4{margin:0 0 6px;font-size:15px;font-weight:600;color:#5a1616}
            .shop-trust__card p{margin:0;font-size:13px;line-height:1.55;color:rgba(58,42,36,.78)}
            .shop-trust__cta{display:flex;flex-wrap:wrap;align-items:center;gap:10px;margin-top:20px;padding-top:16px;border-top:1px solid rgba(109,23,23,.1);font-size:13px;color:rgba(58,42,36,.55)}
            .shop-trust__cta a{color:#9c2f24;font-weight:600;text-decoration:none;border-bottom:1px solid rgba(156,47,36,.35)}
            .shop-trust__cta a:hover{border-bottom-color:#9c2f24}
            @media (max-width:991px){.shop-trust__grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
            @media (max-width:575px){.shop-trust{padding:22px 16px}.shop-trust__grid{grid-template-columns:minmax(0,1fr)}.shop-trust__title{font-size:20px}}
            </style>
            <section class="shop-trust">
            	<div class="shop-trust__head">
            		<span class="shop-trust__eyebrow">Chăm sóc &amp; cam kết</span>
            		<h3 class="shop-trust__title">An tâm khi chọn sản phẩm này</h3>
            		<p class="shop-trust__lead">Mỗi món gốm được kiểm tra kỹ trước khi đóng gói — để bạn nhận hàng đẹp, dùng lâu và dễ bảo quản.</p>
            	</div>
            	<div class="shop-trust__grid">
            		<article class="shop-trust__card">
            			<span class="shop-trust__icon" aria-hidden="true"><i class="fa fa-shield"></i></span>
            			<h4>Chính hãng</h4>
            			<p>Nguồn gốc rõ ràng, men phủ đều, không pha hàng kém chất lượng.</p>
            		</article>
            		<article class="shop-trust__card">
            			<span class="shop-trust__icon" aria-hidden="true"><i class="fa fa-gift"></i></span>
            			<h4>Đóng gói kỹ</h4>
            			<p>Bọc xốp nhiều lớp, chống sốc khi vận chuyển đường dài.</p>
            		</article>
            		<article class="shop-trust__card">
            			<span class="shop-trust__icon" aria-hidden="true"><i class="fa fa-refresh"></i></span>
            			<h4>Đổi trả linh hoạt</h4>
            			<p>Hỗ trợ đổi trả theo chính sách nếu sản phẩm lỗi do vận chuyển.</p>
            		</article>
            		<article class="shop-trust__card">
            			<span class="shop-trust__icon" aria-hidden="true"><i class="fa fa-leaf"></i></span>
            			<h4>Bảo quản đơn giản</h4>
            			<p>Lau khô sau khi dùng, tránh sốc nhiệt — giữ men bóng đẹp lâu dài.</p>
            		</article>
            	</div>
            	<div class="shop-trust__cta">
            		<a href="/chinh-sach-doi-tra">Xem chính sách đổi trả</a>
            		<span>·</span>
            		<a href="/chinh-sach-giao-hang">Chính sách giao hàng</a>
            	</div>
            </section>
            """,
            "Chỉ hiện khi chèn {{block:product-trust}} vào mô tả từng sản phẩm.");

        await ScopeProductShortcodesOnceAsync(db);
        await EnsureYoutubePrivacyAsync(db);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Click-to-play + youtube-nocookie — HTML/CSS/JS nằm trong HtmlBlock (Admin sửa được).
    /// </summary>
    public static async Task EnsureYoutubePrivacyAsync(AppDbContext db)
    {
        const string key = "home-youtube";
        const string note = "Tự chứa: style + click-to-play + youtube-nocookie trong block. Sửa tại Admin → Html blocks.";
        const string marker = "yt-privacy-v2";
        var content = YoutubeBlockHtml();

        var block = await db.HtmlBlocks.FirstOrDefaultAsync(item => item.Key == key);
        if (block is null)
        {
            db.HtmlBlocks.Add(new HtmlBlock
            {
                Key = key,
                Title = "YouTube Tự hào Gốm Việt (trang chủ)",
                Content = content,
                Note = note,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        var needsUpgrade = block.Content.Contains("youtube.com/embed", StringComparison.OrdinalIgnoreCase)
            || !block.Content.Contains("data-yt=", StringComparison.Ordinal)
            || !block.Content.Contains(marker, StringComparison.Ordinal);
        if (!needsUpgrade)
            return;

        block.Content = content;
        block.Note = note;
        block.IsActive = true;
        block.UpdatedAt = DateTime.UtcNow;
    }

    private static string YoutubeBlockHtml() =>
        """
        <!-- yt-privacy-v2 -->
        <style>
        .shop-youtube{margin:36px 0 28px}
        .shop-youtube__lead{margin:-6px 0 18px;max-width:52ch;font-size:14px;color:rgba(43,38,34,.72)}
        .shop-youtube__grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}
        .shop-youtube__frame{position:relative;aspect-ratio:16/9;overflow:hidden;background:#1a0c0a;border-radius:4px}
        .shop-youtube__frame iframe{position:absolute;inset:0;width:100%;height:100%;border:0}
        .shop-youtube__facade{position:absolute;inset:0;display:block;width:100%;height:100%;padding:0;border:0;cursor:pointer;background:#1a0c0a}
        .shop-youtube__facade img{width:100%;height:100%;object-fit:cover;display:block}
        .shop-youtube__play{position:absolute;left:50%;top:50%;width:64px;height:64px;margin:-32px 0 0 -32px;border-radius:50%;background:rgba(122,31,31,.92);box-shadow:0 4px 16px rgba(0,0,0,.35);pointer-events:none}
        .shop-youtube__play::before{content:"";position:absolute;left:26px;top:20px;border-style:solid;border-width:12px 0 12px 20px;border-color:transparent transparent transparent #fff}
        .shop-youtube__facade:hover .shop-youtube__play,.shop-youtube__facade:focus-visible .shop-youtube__play{background:rgba(90,16,16,.96);transform:scale(1.04)}
        @media (max-width:991px){.shop-youtube__grid{grid-template-columns:1fr}}
        </style>
        <section class="shop-youtube" aria-label="Tự hào Gốm Việt">
        	<h2 class="shop-title">Tự hào Gốm Việt</h2>
        	<p class="shop-youtube__lead">Câu chuyện gốm sứ và bàn trà — xem thêm trên kênh YouTube cửa hàng.</p>
        	<div class="shop-youtube__grid">
        		<div class="shop-youtube__frame" data-yt="aymsvfB8HvI">
        			<button type="button" class="shop-youtube__facade" aria-label="Phát video Tự hào Gốm Việt">
        				<img src="https://i.ytimg.com/vi/aymsvfB8HvI/hqdefault.jpg" alt="" width="480" height="360" loading="lazy" decoding="async" />
        				<span class="shop-youtube__play" aria-hidden="true"></span>
        			</button>
        		</div>
        		<div class="shop-youtube__frame" data-yt="lxuQanY7LwQ">
        			<button type="button" class="shop-youtube__facade" aria-label="Phát video Tự hào Gốm Việt">
        				<img src="https://i.ytimg.com/vi/lxuQanY7LwQ/hqdefault.jpg" alt="" width="480" height="360" loading="lazy" decoding="async" />
        				<span class="shop-youtube__play" aria-hidden="true"></span>
        			</button>
        		</div>
        		<div class="shop-youtube__frame" data-yt="mQLhC0XaOW8">
        			<button type="button" class="shop-youtube__facade" aria-label="Phát video Tự hào Gốm Việt">
        				<img src="https://i.ytimg.com/vi/mQLhC0XaOW8/hqdefault.jpg" alt="" width="480" height="360" loading="lazy" decoding="async" />
        				<span class="shop-youtube__play" aria-hidden="true"></span>
        			</button>
        		</div>
        	</div>
        </section>
        <script>
        (function(){
        	var frames=document.querySelectorAll(".shop-youtube__frame[data-yt]");
        	if(!frames.length)return;
        	function play(frame){
        		var id=frame.getAttribute("data-yt");
        		if(!id||frame.querySelector("iframe"))return;
        		var btn=frame.querySelector(".shop-youtube__facade");
        		var title=btn&&btn.getAttribute("aria-label")||"Video YouTube";
        		var iframe=document.createElement("iframe");
        		iframe.src="https://www.youtube-nocookie.com/embed/"+encodeURIComponent(id)+"?autoplay=1&rel=0";
        		iframe.title=title;
        		iframe.allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share";
        		iframe.allowFullscreen=true;
        		iframe.loading="lazy";
        		frame.replaceChildren(iframe);
        	}
        	frames.forEach(function(frame){
        		frame.addEventListener("click",function(e){e.preventDefault();play(frame);});
        		frame.addEventListener("keydown",function(e){
        			if(e.key!=="Enter"&&e.key!==" ")return;
        			e.preventDefault();play(frame);
        		});
        	});
        })();
        </script>
        """.Trim();

    private static async Task ScopeProductShortcodesOnceAsync(AppDbContext db)
    {
        if (await db.SystemSettings.AnyAsync(item => item.Key == ScopedMarker && item.Value == "1"))
            return;

        var products = await db.Products.OrderBy(item => item.Id).ToListAsync();
        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.Description))
                continue;
            product.Description = StripShortcode(product.Description);
        }

        var sample = products.FirstOrDefault(item => item.IsVisible) ?? products.FirstOrDefault();
        if (sample is not null)
        {
            var body = (sample.Description ?? string.Empty).TrimEnd();
            sample.Description = string.IsNullOrWhiteSpace(body)
                ? ProductTrustShortcode
                : body + "\n\n" + ProductTrustShortcode;
        }

        db.SystemSettings.Add(new SystemSetting
        {
            Key = ScopedMarker,
            Value = "1",
            Description = "Đã gỡ shortcode product-trust khỏi mọi SP; chỉ giữ mẫu trên 1 sản phẩm"
        });
    }

    private static string StripShortcode(string content)
    {
        var cleaned = content
            .Replace("\r\n" + ProductTrustShortcode, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\n" + ProductTrustShortcode, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(ProductTrustShortcode, string.Empty, StringComparison.OrdinalIgnoreCase);
        return cleaned.Trim();
    }

    private static async Task ForceUpsertAsync(AppDbContext db, string key, string title, string content, string note)
    {
        var existing = await db.HtmlBlocks.FirstOrDefaultAsync(block => block.Key == key);
        if (existing is null)
        {
            db.HtmlBlocks.Add(new HtmlBlock
            {
                Key = key,
                Title = title,
                Content = content.Trim(),
                Note = note,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        existing.Title = title;
        existing.Content = content.Trim();
        existing.Note = note;
        existing.IsActive = true;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task EnsureTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "HtmlBlocks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_HtmlBlocks" PRIMARY KEY AUTOINCREMENT,
                "Key" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "Note" TEXT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "UpdatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_HtmlBlocks_Key" ON "HtmlBlocks" ("Key");
            """);
    }
}
