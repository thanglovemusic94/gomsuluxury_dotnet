using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Posts;

public class EditModel(AppDbContext db, AuditService audit) : PageModel
{
    [BindProperty]
    public PostInput Input { get; set; } = new();

    [BindProperty]
    public SeoInput Seo { get; set; } = new();

    public IList<Category> Categories { get; private set; } = [];

    public IList<User> Authors { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadAsync();
        if (id is null)
            return Page();

        var post = await db.Posts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (post is null)
            return NotFound();

        Input = PostInput.From(post);
        Seo = SeoInput.From(post);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        Input.Title = TextHelper.Trimmed(Input.Title);
        Input.Content = TextHelper.Trimmed(Input.Content);
        if (Input.Title.Length is < 1 or > 250)
            ModelState.AddModelError("Input.Title", "Tiêu đề từ 1 đến 250 ký tự.");
        if (string.IsNullOrWhiteSpace(Input.Content))
            ModelState.AddModelError("Input.Content", "Nhập nội dung.");
        if (Input.Summary?.Length > 500)
            ModelState.AddModelError("Input.Summary", "Tóm tắt tối đa 500 ký tự.");
        if (Input.ImageUrl?.Length > 500)
            ModelState.AddModelError("Input.ImageUrl", "Ảnh tối đa 500 ký tự.");
        if (!Categories.Any(category => category.Id == Input.CategoryId))
            ModelState.AddModelError("Input.CategoryId", "Chọn danh mục loại Blog.");
        if (!Authors.Any(author => author.Id == Input.AuthorId))
            ModelState.AddModelError("Input.AuthorId", "Chọn tác giả.");
        Seo.Validate(ModelState);

        var slug = TextHelper.Slug(Input.Slug, Input.Title);
        if (slug.Length > 255)
            ModelState.AddModelError("Input.Slug", "Slug tối đa 255 ký tự.");
        if (await db.Posts.AnyAsync(post => post.Slug == slug && post.Id != Input.Id))
            ModelState.AddModelError("Input.Slug", "Slug đã tồn tại.");
        if (!ModelState.IsValid)
            return Page();

        Post postEntity;
        var isCreate = Input.Id == 0;
        Dictionary<string, string?>? before = null;
        if (isCreate)
        {
            postEntity = new Post();
            db.Posts.Add(postEntity);
        }
        else
        {
            var existing = await db.Posts.FirstOrDefaultAsync(post => post.Id == Input.Id);
            if (existing is null)
                return NotFound();
            postEntity = existing;
            before = SnapshotPost(postEntity);
            postEntity.UpdatedAt = DateTime.UtcNow;
        }

        Input.Apply(postEntity, slug);
        Seo.SeoScore = SeoScoreCalculator.Compute(new SeoScoreCalculator.Request(
            Title: postEntity.Title,
            Slug: postEntity.Slug,
            FocusKeyword: Seo.SeoFocusKeyword,
            MetaTitle: Seo.MetaTitle,
            MetaDescription: Seo.MetaDescription,
            SeoImage: Seo.SeoImage,
            ImageUrl: postEntity.ImageUrl,
            ShortText: postEntity.Summary,
            HtmlBody: postEntity.Content,
            ImageAlts: null));
        Seo.Apply(postEntity);
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        if (isCreate)
            await audit.LogAsync(AuditActions.Create, AuditEntities.Post, postEntity.Id, postEntity.Title, "Tạo bài viết mới");
        else if (before is not null)
        {
            var changes = AuditService.Diff(before, SnapshotPost(postEntity));
            if (changes.Count > 0)
                await audit.LogAsync(AuditActions.Update, AuditEntities.Post, postEntity.Id, postEntity.Title,
                    $"Sửa {changes.Count} trường", string.Join("\n", changes));
        }

        TempData["Message"] = "Đã lưu bài viết.";
        return RedirectToPage("Index");
    }

    private static Dictionary<string, string?> SnapshotPost(Post post) => new()
    {
        ["Title"] = post.Title,
        ["Slug"] = post.Slug,
        ["Summary"] = post.Summary,
        ["Content"] = post.Content,
        ["ImageUrl"] = post.ImageUrl,
        ["IsPublished"] = post.IsPublished.ToString(),
        ["CategoryId"] = post.CategoryId.ToString(),
        ["AuthorId"] = post.AuthorId.ToString()
    };

    private async Task LoadAsync()
    {
        Categories = await db.Categories.AsNoTracking().Where(category => category.Type == "Blog").OrderBy(category => category.Name).ToListAsync();
        Authors = await db.Users.AsNoTracking().Where(user => user.IsActive).OrderBy(user => user.Username).ToListAsync();
    }

    public class PostInput
    {
        public int Id { get; set; }

        public int CategoryId { get; set; }

        public int AuthorId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string? Summary { get; set; }

        public string Content { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public bool IsPublished { get; set; }

        public static PostInput From(Post post) => new()
        {
            Id = post.Id,
            CategoryId = post.CategoryId,
            AuthorId = post.AuthorId,
            Title = post.Title,
            Slug = post.Slug,
            Summary = post.Summary,
            Content = post.Content,
            ImageUrl = post.ImageUrl,
            IsPublished = post.IsPublished
        };

        public void Apply(Post post, string slug)
        {
            post.CategoryId = CategoryId;
            post.AuthorId = AuthorId;
            post.Title = Title;
            post.Slug = slug;
            post.Summary = TextHelper.Clean(Summary);
            post.Content = Content;
            post.ImageUrl = TextHelper.Clean(ImageUrl);
            post.IsPublished = IsPublished;
        }
    }
}
