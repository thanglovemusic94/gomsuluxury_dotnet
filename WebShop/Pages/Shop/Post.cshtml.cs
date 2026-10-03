using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class PostPageModel(AppDbContext db) : PageModel
{
    public Post? Post { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Post = await db.Posts.AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.Author)
            .FirstOrDefaultAsync(item => item.IsPublished && item.Slug == slug);
        if (Post is null)
            return NotFound();

        ViewData["Title"] = Post.MetaTitle ?? Post.Title;
        ViewData["Description"] = Post.MetaDescription ?? Post.Summary;
        ViewData["OgType"] = "article";
        ViewData["OgImage"] = SiteSocial.PickImage(Post.SeoImage, Post.ImageUrl);
        if (HttpContext.Request is { } req)
        {
            var snapName = await db.SystemSettings.AsNoTracking()
                .Where(item => item.Key == "SiteName")
                .Select(item => item.Value)
                .FirstOrDefaultAsync() ?? "WebShop";
            var url = SiteSocial.Absolute(req, "/blog/" + Post.Slug);
            ViewData["JsonLd"] = SchemaOrg.Serialize(SchemaOrg.BlogPosting(req, Post, snapName, url));
        }
        return Page();
    }
}
