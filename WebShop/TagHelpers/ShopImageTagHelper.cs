using Microsoft.AspNetCore.Razor.TagHelpers;
using WebShop.Infrastructure;

namespace WebShop.TagHelpers;

/// <summary>
/// Sharp responsive image for slider / gallery / product card / blog.
/// AVIF omitted: ImageSharp 3 has no built-in AVIF encoder — WebP presets + OTF retina.
/// </summary>
/// <example>
/// <shop-img src="@url" profile="card" alt="..." loading="lazy" />
/// </example>
[HtmlTargetElement("shop-img", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ShopImageTagHelper(MediaOriginalLookup originals) : TagHelper
{
    /// <summary>Media URL (optimized or original).</summary>
    public string? Src { get; set; }

    /// <summary>home-slider | slider | gallery | card | blog-card | post-cover</summary>
    public string Profile { get; set; } = "gallery";

    public string? Alt { get; set; }

    public string? Class { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? Loading { get; set; }

    [HtmlAttributeName("fetch-priority")]
    public string? FetchPriority { get; set; }

    public string? Decoding { get; set; } = "async";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "img";
        output.TagMode = TagMode.StartTagOnly;

        var profile = (Profile ?? "gallery").Trim().ToLowerInvariant();
        var original = originals.Resolve(Src);
        var (src, srcset, sizes, defaultW, defaultH) = profile switch
        {
            "home-slider" or "slider-home" => (
                MediaUrls.HeroSrc(Src),
                MediaUrls.SrcSetHero(Src, original),
                MediaUrls.SizesHomeSlider,
                1600,
                900),
            "slider" => (
                MediaUrls.HeroSrc(Src),
                MediaUrls.SrcSetHero(Src, original),
                MediaUrls.SizesFullSlider,
                1600,
                900),
            "card" or "product-card" => (
                MediaUrls.CardSrc(Src),
                MediaUrls.SrcSetCard(Src),
                MediaUrls.SizesProductCard,
                800,
                800),
            "blog-card" => (
                MediaUrls.CardSrc(Src),
                MediaUrls.SrcSetBlogCard(Src),
                MediaUrls.SizesBlogCard,
                800,
                500),
            "post-cover" => (
                MediaUrls.HeroSrc(Src),
                MediaUrls.SrcSetPostCover(Src, original),
                MediaUrls.SizesPostCover,
                1200,
                750),
            _ => (
                MediaUrls.GallerySrc(Src),
                MediaUrls.SrcSetDetail(Src, original),
                MediaUrls.SizesProductGallery,
                1600,
                1600)
        };

        if (string.IsNullOrWhiteSpace(src))
        {
            output.SuppressOutput();
            return;
        }

        output.Attributes.SetAttribute("src", src);
        if (!string.IsNullOrWhiteSpace(srcset))
            output.Attributes.SetAttribute("srcset", srcset);
        if (!string.IsNullOrWhiteSpace(sizes))
            output.Attributes.SetAttribute("sizes", sizes);

        output.Attributes.SetAttribute("alt", Alt ?? string.Empty);
        output.Attributes.SetAttribute("width", (Width ?? defaultW).ToString());
        output.Attributes.SetAttribute("height", (Height ?? defaultH).ToString());
        output.Attributes.SetAttribute("decoding", string.IsNullOrWhiteSpace(Decoding) ? "async" : Decoding);

        if (!string.IsNullOrWhiteSpace(Class))
            output.Attributes.SetAttribute("class", Class);
        if (!string.IsNullOrWhiteSpace(Loading))
            output.Attributes.SetAttribute("loading", Loading);
        if (!string.IsNullOrWhiteSpace(FetchPriority))
            output.Attributes.SetAttribute("fetchpriority", FetchPriority);
    }
}
