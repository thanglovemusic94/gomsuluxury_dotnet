using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WebShop.Data;
using WebShop.Infrastructure;

var culture = (CultureInfo)CultureInfo.GetCultureInfo("vi-VN").Clone();
culture.NumberFormat.NumberDecimalSeparator = ".";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
culture.NumberFormat.NumberGroupSeparator = ",";
culture.NumberFormat.CurrencyGroupSeparator = ",";
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.Name = "WebShop.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        // Mặc định cho cookie persistent (tick “Ghi nhớ đăng nhập”); session cookie khi không tick.
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ShopStore>();
builder.Services.AddScoped<AuditService>();
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection("Media"));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<SeoOptions>(builder.Configuration.GetSection(SeoOptions.SectionName));
builder.Services.AddScoped<SiteSeoService>();
builder.Services.AddSingleton<MediaStorage>();
builder.Services.AddSingleton<MediaUsageService>();
builder.Services.AddScoped<MediaFolderService>();
builder.Services.AddHostedService<MediaGarbageCollector>();
builder.Services.AddShopImageSharp();
builder.Services.AddScoped<MediaTrashFilter>();
builder.Services.AddScoped<HtmlBlockService>();
builder.Services.AddScoped<GeminiSeoService>();
builder.Services.AddScoped<TemporaryCartService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("media-import", client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WebShopMediaImport/1.0");
});
builder.Services.AddHttpClient("gemini", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WebShopGeminiSeo/1.0");
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOrStaff", policy =>
        policy.RequireRole(AppRoles.Admin, AppRoles.Staff, AppRoles.Viewer));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AppRoles.Admin));
    options.AddPolicy("AdminOrViewer", policy =>
        policy.RequireRole(AppRoles.Admin, AppRoles.Viewer));
});
// Production: minify/bundle CSS. Development giữ file gốc để sửa dễ.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddWebOptimizer(pipeline =>
    {
        pipeline.AddCssBundle(
            "/css/shop-ads.bundle.css",
            "css/shop-base.css",
            "css/shop-ads.css");
        // Cùng path wwwroot — response minify (storefront + Gift Ads).
        pipeline.MinifyCssFiles("css/shop.css", "css/shop-ads.css");
    });
}

builder.Services.AddRazorPages(options =>
{
    // Admin + Staff + Viewer: vào khu vực Admin
    options.Conventions.AuthorizeFolder("/Admin", "AdminOrStaff");
    // Admin + Viewer (đọc): thành viên, phân quyền, cấu hình… — Staff không vào
    options.Conventions.AuthorizeFolder("/Admin/Users", "AdminOrViewer");
    options.Conventions.AuthorizeFolder("/Admin/Roles", "AdminOrViewer");
    options.Conventions.AuthorizeFolder("/Admin/Settings", "AdminOrViewer");
    options.Conventions.AuthorizeFolder("/Admin/Menus", "AdminOrViewer");
    options.Conventions.AuthorizeFolder("/Admin/Trash", "AdminOrViewer");
    options.Conventions.AuthorizeFolder("/Admin/AuditLogs", "AdminOrViewer");
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Nginx on same host / VPS — trust X-Forwarded-Proto so Absolute URLs are https.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

var urls = app.Configuration["ASPNETCORE_URLS"] ?? string.Empty;
var httpsPort = app.Configuration["ASPNETCORE_HTTPS_PORT"];
if (!string.IsNullOrEmpty(httpsPort) || urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
    app.UseHttpsRedirection();

var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
// On-the-fly resize/format for /uploads/originals/*?width=&format= (before static files).
app.UseShopImageSharp();
if (!app.Environment.IsDevelopment())
    app.UseWebOptimizer();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/uploads/optimized/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/ace/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=604800";
        }
    }
});

var acePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "ace"));
if (Directory.Exists(acePath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(acePath),
        RequestPath = "/ace",
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Viewer: xem Admin được, không ghi trong /Admin.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
        && AppRoles.IsReadOnlyAdmin(context.User)
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Tài khoản Viewer chỉ được xem Admin, không thực hiện thao tác.");
        return;
    }

    await next();
});

// Staging / pre-launch: discourage search engines when Admin tắt index.
app.Use(async (context, next) =>
{
    var seo = context.RequestServices.GetRequiredService<SiteSeoService>();
    var allow = await seo.GetAllowIndexingAsync(context.RequestAborted);
    context.Items[SiteSeoService.AllowIndexingKey] = allow;
    if (!allow)
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});

app.MapGet("/robots.txt", async (SiteSeoService seo) =>
{
    var allow = await seo.GetAllowIndexingAsync();
    var body = allow
        ? """
          User-agent: *
          Allow: /
          Disallow: /Admin
          Disallow: /Login
          Disallow: /AccessDenied
          Disallow: /api
          """
        : """
          User-agent: *
          Disallow: /
          """;
    return Results.Text(body.Replace("\r\n", "\n").Trim() + "\n", "text/plain; charset=utf-8");
});

// SEO: URL cũ /p/{slug} → /{slug}
app.MapGet("/p", () => Results.Redirect("/", permanent: true));
app.MapGet("/p/{*slug}", (string slug) =>
{
    var clean = PagePaths.NormalizeSlug(slug);
    return string.IsNullOrEmpty(clean)
        ? Results.Redirect("/", permanent: true)
        : Results.Redirect("/" + clean, permanent: true);
});

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var media = scope.ServiceProvider.GetRequiredService<MediaStorage>();
    var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RemoteImageImport");
    var seed = app.Configuration.GetSection("Seed");
    await media.EnsureSchemaAsync();
    await SoftDeleteSchema.EnsureAsync(db);
    await scope.ServiceProvider.GetRequiredService<TemporaryCartService>().EnsureSchemaAsync();
    await AdminSeed.EnsureAdminAsync(db);
    await scope.ServiceProvider.GetRequiredService<SiteSeoService>().EnsureSeedAsync();
    if (seed.GetValue("DemoCatalog", true))
        await DemoCatalog.EnsureAsync(db);
    if (seed.GetValue("LuxuryCatalog", true))
        await LuxuryCatalog.EnsureAsync(db);
    await ProductCategorySchema.EnsureAsync(db);
    await CategorySchema.EnsureAsync(db);
    await ProductReviewSchema.EnsureAsync(db);
    await ProductSchema.EnsureAsync(db);
    await ShopNavSeed.EnsureAsync(db);
    await HtmlBlockSeed.EnsureAsync(db);
    await GiftLandingSeed.EnsureAsync(db);
    await LandingPageSchema.EnsureAsync(db);
    await PagePaths.MigrateLegacyPrefixAsync(db);
    if (seed.GetValue("SourceSiteSync", true))
    {
        var syncLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SourceSiteSync");
        await SourceSiteSync.EnsureAsync(db, syncLogger);
    }
    if (seed.GetValue("RemoteImageImport", true))
        await RemoteImageImport.EnsureAsync(db, media, httpFactory, logger);
    await MediaFolderBackfill.EnsureAsync(db, media, logger);
    await MediaOptimizeBackfill.EnsureAsync(db, media, logger);
}

app.Run();
