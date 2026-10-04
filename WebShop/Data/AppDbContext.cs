using Microsoft.EntityFrameworkCore;
using WebShop.Models;

namespace WebShop.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<CustomPage> CustomPages => Set<CustomPage>();

    public DbSet<Menu> Menus => Set<Menu>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<HtmlBlock> HtmlBlocks => Set<HtmlBlock>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<TemporaryCart> TemporaryCarts => Set<TemporaryCart>();

    public DbSet<LandingPage> LandingPages => Set<LandingPage>();

    public DbSet<LandingPageProduct> LandingPageProducts => Set<LandingPageProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigureUserRole(modelBuilder);
        ConfigureCategory(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureProductCategory(modelBuilder);
        ConfigureProductImage(modelBuilder);
        ConfigureProductReview(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigureOrderItem(modelBuilder);
        ConfigurePost(modelBuilder);
        ConfigureCustomPage(modelBuilder);
        ConfigureMenu(modelBuilder);
        ConfigureSystemSetting(modelBuilder);
        ConfigureHtmlBlock(modelBuilder);
        ConfigureMediaAsset(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureTemporaryCart(modelBuilder);
        ConfigureLandingPage(modelBuilder);
        ConfigureLandingPageProduct(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Username).HasMaxLength(50).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(150).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(100);
            entity.Property(user => user.PhoneNumber).HasMaxLength(15);
            entity.Property(user => user.IsActive).HasDefaultValue(true);
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
        });
    }

    private static void ConfigureRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(role => role.Name).HasMaxLength(50).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(255);
            entity.HasIndex(role => role.Name).IsUnique();

            entity.HasData(
                new Role { Id = 1, Name = "Admin", Description = "Quản trị toàn hệ thống" },
                new Role { Id = 2, Name = "Staff", Description = "Nhân viên vận hành đơn hàng và nội dung" },
                new Role { Id = 3, Name = "Customer", Description = "Khách hàng" });
        });
    }

    private static void ConfigureUserRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasMany(user => user.Roles)
            .WithMany(role => role.Users)
            .UsingEntity<UserRole>(
                right => right
                    .HasOne(userRole => userRole.Role)
                    .WithMany(role => role.UserRoles)
                    .HasForeignKey(userRole => userRole.RoleId)
                    .OnDelete(DeleteBehavior.Restrict),
                left => left
                    .HasOne(userRole => userRole.User)
                    .WithMany(user => user.UserRoles)
                    .HasForeignKey(userRole => userRole.UserId)
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("UserRoles");
                    join.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
                });
    }

    private static void ConfigureCategory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.Property(category => category.Slug).HasMaxLength(150).IsRequired();
            entity.Property(category => category.Type).HasMaxLength(20).IsRequired();
            entity.Property(category => category.IsVisible).HasDefaultValue(true);
            entity.HasIndex(category => category.Slug).IsUnique();

            entity.HasOne(category => category.Parent)
                .WithMany(category => category.Children)
                .HasForeignKey(category => category.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Category_Type",
                "[Type] IN ('Product', 'Blog')"));
        });
    }

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Slug).HasMaxLength(255).IsRequired();
            entity.Property(product => product.Price).HasPrecision(18, 2);
            entity.Property(product => product.DiscountPrice).HasPrecision(18, 2);
            entity.Property(product => product.CostPrice).HasPrecision(18, 2);
            entity.Property(product => product.Stock).HasDefaultValue(0);
            entity.Property(product => product.Description).IsRequired();
            entity.Property(product => product.ShortDescription).HasMaxLength(500);
            entity.Property(product => product.ImageUrl).HasMaxLength(500).IsRequired();
            entity.Property(product => product.IsVisible).HasDefaultValue(true);
            entity.Property(product => product.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            ConfigureSeo(entity);
            entity.HasIndex(product => product.Slug).IsUnique();

            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(product => !product.IsDeleted);
            entity.HasIndex(product => product.IsDeleted);
        });
    }

    private static void ConfigureProductCategory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasMany(product => product.ProductCategories)
            .WithOne(link => link.Product)
            .HasForeignKey(link => link.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Category>()
            .HasMany(category => category.ProductCategories)
            .WithOne(link => link.Category)
            .HasForeignKey(link => link.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("ProductCategories");
            entity.HasKey(link => new { link.ProductId, link.CategoryId });
            entity.HasIndex(link => link.CategoryId);
        });
    }

    private static void ConfigureProductImage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.Property(image => image.ImageUrl).HasMaxLength(500).IsRequired();
            entity.Property(image => image.DisplayOrder).HasDefaultValue(0);
            entity.Property(image => image.AltText).HasMaxLength(200);

            entity.HasOne(image => image.Product)
                .WithMany(product => product.Images)
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureProductReview(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.Property(review => review.CustomerName).HasMaxLength(100).IsRequired();
            entity.Property(review => review.Comment).HasMaxLength(1000).IsRequired();
            entity.Property(review => review.ImageUrl).HasMaxLength(500);
            entity.Property(review => review.Source).HasMaxLength(40).IsRequired().HasDefaultValue("Web");
            entity.Property(review => review.IsApproved).HasDefaultValue(false);
            entity.Property(review => review.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(review => review.Product)
                .WithMany(product => product.Reviews)
                .HasForeignKey(review => review.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(review => review.User)
                .WithMany(user => user.Reviews)
                .HasForeignKey(review => review.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ProductReview_Rating",
                "[Rating] >= 1 AND [Rating] <= 5"));
        });
    }

    private static void ConfigureOrder(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(order => order.OrderCode).HasMaxLength(50).IsRequired();
            entity.Property(order => order.OrderDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(order => order.TotalAmount).HasPrecision(18, 2);
            entity.Property(order => order.Status).HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
            entity.Property(order => order.PaymentMethod).HasMaxLength(50).IsRequired();
            entity.Property(order => order.PaymentStatus).HasMaxLength(30).IsRequired().HasDefaultValue("Unpaid");
            entity.Property(order => order.CustomerName).HasMaxLength(100).IsRequired();
            entity.Property(order => order.CustomerPhone).HasMaxLength(15).IsRequired();
            entity.Property(order => order.ShippingAddress).HasMaxLength(500).IsRequired();
            entity.Property(order => order.OrderNote).HasMaxLength(500);
            entity.Property(order => order.Source).HasMaxLength(100);
            entity.HasIndex(order => order.OrderCode).IsUnique();

            entity.HasOne(order => order.User)
                .WithMany(user => user.Orders)
                .HasForeignKey(order => order.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Order_Status",
                    "[Status] IN ('Pending', 'Confirmed', 'Shipping', 'Completed', 'Cancelled')");
                table.HasCheckConstraint(
                    "CK_Order_PaymentStatus",
                    "[PaymentStatus] IN ('Unpaid', 'Paid')");
            });
        });
    }

    private static void ConfigureOrderItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.OriginalCostPrice).HasPrecision(18, 2);

            entity.HasOne(item => item.Order)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Product)
                .WithMany(product => product.OrderItems)
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePost(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>(entity =>
        {
            entity.Property(post => post.Title).HasMaxLength(250).IsRequired();
            entity.Property(post => post.Slug).HasMaxLength(255).IsRequired();
            entity.Property(post => post.Summary).HasMaxLength(500);
            entity.Property(post => post.Content).IsRequired();
            entity.Property(post => post.ImageUrl).HasMaxLength(500);
            entity.Property(post => post.IsPublished).HasDefaultValue(false);
            entity.Property(post => post.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            ConfigureSeo(entity);
            entity.HasIndex(post => post.Slug).IsUnique();

            entity.HasOne(post => post.Category)
                .WithMany(category => category.Posts)
                .HasForeignKey(post => post.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(post => post.Author)
                .WithMany(user => user.Posts)
                .HasForeignKey(post => post.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(post => !post.IsDeleted);
            entity.HasIndex(post => post.IsDeleted);
        });
    }

    private static void ConfigureCustomPage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomPage>(entity =>
        {
            entity.Property(page => page.Title).HasMaxLength(250).IsRequired();
            entity.Property(page => page.Slug).HasMaxLength(255).IsRequired();
            entity.Property(page => page.Content).IsRequired();
            entity.Property(page => page.IsPublished).HasDefaultValue(true);
            entity.Property(page => page.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            ConfigureSeo(entity);
            entity.HasIndex(page => page.Slug).IsUnique();
            entity.HasQueryFilter(page => !page.IsDeleted);
            entity.HasIndex(page => page.IsDeleted);
        });
    }

    private static void ConfigureMenu(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.Property(menu => menu.Name).HasMaxLength(100).IsRequired();
            entity.Property(menu => menu.Url).HasMaxLength(255).IsRequired();
            entity.Property(menu => menu.DisplayOrder).HasDefaultValue(0);
            entity.Property(menu => menu.Position).HasMaxLength(50).IsRequired().HasDefaultValue("Header");
            entity.Property(menu => menu.IsActive).HasDefaultValue(true);

            entity.HasOne(menu => menu.Parent)
                .WithMany(menu => menu.Children)
                .HasForeignKey(menu => menu.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Menu_Position",
                "[Position] IN ('Header', 'Footer')"));
        });
    }

    private static void ConfigureSystemSetting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.Property(setting => setting.Key).HasMaxLength(100).IsRequired();
            entity.Property(setting => setting.Description).HasMaxLength(255);
            entity.HasIndex(setting => setting.Key).IsUnique();
        });
    }

    private static void ConfigureHtmlBlock(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HtmlBlock>(entity =>
        {
            entity.Property(block => block.Key).HasMaxLength(100).IsRequired();
            entity.Property(block => block.Title).HasMaxLength(200).IsRequired();
            entity.Property(block => block.Content).IsRequired();
            entity.Property(block => block.Note).HasMaxLength(255);
            entity.Property(block => block.IsActive).HasDefaultValue(true);
            entity.Property(block => block.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(block => block.Key).IsUnique();
        });
    }

    private static void ConfigureMediaAsset(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.Property(item => item.FileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Alt).HasMaxLength(255);
            entity.Property(item => item.Folder).HasMaxLength(50).IsRequired().HasDefaultValue("khac");
            entity.Property(item => item.OriginalPath).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OriginalUrl).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ThumbPath).HasMaxLength(500);
            entity.Property(item => item.ThumbUrl).HasMaxLength(500);
            entity.Property(item => item.MediumPath).HasMaxLength(500);
            entity.Property(item => item.MediumUrl).HasMaxLength(500);
            entity.Property(item => item.LargePath).HasMaxLength(500);
            entity.Property(item => item.LargeUrl).HasMaxLength(500);
            entity.Property(item => item.ContentHash).HasMaxLength(64);
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => item.Folder);
            entity.HasIndex(item => item.ContentHash);
            entity.HasQueryFilter(item => !item.IsDeleted);
            entity.HasIndex(item => item.IsDeleted);
        });
    }

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(item => item.Username).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(40).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.EntityTitle).HasMaxLength(255);
            entity.Property(item => item.Summary).HasMaxLength(500);
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => new { item.EntityType, item.EntityId });
        });
    }

    private static void ConfigureTemporaryCart(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemporaryCart>(entity =>
        {
            entity.Property(item => item.Token).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Sku).HasMaxLength(50).IsRequired();
            entity.Property(item => item.CustomerPhone).HasMaxLength(20);
            entity.Property(item => item.FbUserId).HasMaxLength(64);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(item => item.Token).IsUnique();
            entity.HasIndex(item => item.Status);
            entity.HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Order)
                .WithMany()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureLandingPage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LandingPage>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(150).IsRequired();
            entity.Property(item => item.RedirectWhenOff).HasMaxLength(20).IsRequired().HasDefaultValue("Home");
            entity.Property(item => item.Headline).HasMaxLength(250).IsRequired();
            entity.Property(item => item.Subheadline).HasMaxLength(500);
            entity.Property(item => item.HeroImageUrl).HasMaxLength(500);
            entity.Property(item => item.CtaText).HasMaxLength(80).IsRequired().HasDefaultValue("Đặt mua ngay");
            entity.Property(item => item.CtaUrl).HasMaxLength(500);
            entity.Property(item => item.ThankYouMessage).HasMaxLength(500).IsRequired();
            entity.Property(item => item.MetaPixelId).HasMaxLength(40);
            entity.Property(item => item.TikTokPixelId).HasMaxLength(40);
            entity.HasIndex(item => item.Slug).IsUnique();
            ConfigureSeo(entity);
        });
    }

    private static void ConfigureLandingPageProduct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LandingPageProduct>(entity =>
        {
            entity.HasKey(item => new { item.LandingPageId, item.ProductId });
            entity.Property(item => item.DisplayOrder).HasDefaultValue(0);
            entity.Property(item => item.AdsPrice).HasColumnType("TEXT");
            entity.HasOne(item => item.LandingPage)
                .WithMany(page => page.Products)
                .HasForeignKey(item => item.LandingPageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSeo<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        entity.Property<string?>("MetaTitle").HasMaxLength(100);
        entity.Property<string?>("MetaDescription").HasMaxLength(255);
        entity.Property<string?>("SeoImage").HasMaxLength(500);
        entity.Property<string?>("SeoFocusKeyword").HasMaxLength(100);
        entity.Property<int>("SeoScore").HasDefaultValue(0);
    }
}
