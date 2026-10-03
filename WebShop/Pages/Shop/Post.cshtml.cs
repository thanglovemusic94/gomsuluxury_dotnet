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
        return Page();
    }
}
