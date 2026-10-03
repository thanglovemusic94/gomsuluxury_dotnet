using Microsoft.AspNetCore.Mvc.ModelBinding;
using WebShop.Models;

namespace WebShop.Infrastructure;

public class SeoInput
{
    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? SeoImage { get; set; }

    public string? SeoFocusKeyword { get; set; }

    public int SeoScore { get; set; }

    public void Validate(ModelStateDictionary modelState)
    {
        if (MetaTitle?.Length > 100)
            modelState.AddModelError("Seo.MetaTitle", "Meta title tối đa 100 ký tự.");
        if (MetaDescription?.Length > 255)
            modelState.AddModelError("Seo.MetaDescription", "Meta description tối đa 255 ký tự.");
        if (SeoImage?.Length > 500)
            modelState.AddModelError("Seo.SeoImage", "Ảnh SEO tối đa 500 ký tự.");
        if (SeoFocusKeyword?.Length > 100)
            modelState.AddModelError("Seo.SeoFocusKeyword", "Từ khóa (các cụm cách nhau bằng dấu phẩy) tối đa 100 ký tự.");
        else if (!string.IsNullOrWhiteSpace(SeoFocusKeyword))
            SeoFocusKeyword = SeoScoreCalculator.JoinKeywords(SeoScoreCalculator.ParseKeywords(SeoFocusKeyword));
        if (SeoFocusKeyword?.Length > 100)
            modelState.AddModelError("Seo.SeoFocusKeyword", "Từ khóa quá dài sau khi chuẩn hóa — bớt cụm hoặc rút ngắn.");
        if (SeoScore is < 0 or > 100)
            modelState.AddModelError("Seo.SeoScore", "Điểm SEO từ 0 đến 100.");
    }

    public void Apply(Product product)
    {
        product.MetaTitle = TextHelper.Clean(MetaTitle);
        product.MetaDescription = TextHelper.Clean(MetaDescription);
        product.SeoImage = TextHelper.Clean(SeoImage);
        product.SeoFocusKeyword = TextHelper.Clean(SeoFocusKeyword);
        product.SeoScore = SeoScore;
    }

    public void Apply(Post post)
    {
        post.MetaTitle = TextHelper.Clean(MetaTitle);
        post.MetaDescription = TextHelper.Clean(MetaDescription);
        post.SeoImage = TextHelper.Clean(SeoImage);
        post.SeoFocusKeyword = TextHelper.Clean(SeoFocusKeyword);
        post.SeoScore = SeoScore;
    }

    public void Apply(CustomPage page)
    {
        page.MetaTitle = TextHelper.Clean(MetaTitle);
        page.MetaDescription = TextHelper.Clean(MetaDescription);
        page.SeoImage = TextHelper.Clean(SeoImage);
        page.SeoFocusKeyword = TextHelper.Clean(SeoFocusKeyword);
        page.SeoScore = SeoScore;
    }

    public static SeoInput From(Product product) => new()
    {
        MetaTitle = product.MetaTitle,
        MetaDescription = product.MetaDescription,
        SeoImage = product.SeoImage,
        SeoFocusKeyword = product.SeoFocusKeyword,
        SeoScore = product.SeoScore
    };

    public static SeoInput From(Post post) => new()
    {
        MetaTitle = post.MetaTitle,
        MetaDescription = post.MetaDescription,
        SeoImage = post.SeoImage,
        SeoFocusKeyword = post.SeoFocusKeyword,
        SeoScore = post.SeoScore
    };

    public static SeoInput From(CustomPage page) => new()
    {
        MetaTitle = page.MetaTitle,
        MetaDescription = page.MetaDescription,
        SeoImage = page.SeoImage,
        SeoFocusKeyword = page.SeoFocusKeyword,
        SeoScore = page.SeoScore
    };
}
