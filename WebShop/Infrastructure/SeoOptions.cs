namespace WebShop.Infrastructure;

/// <summary>
/// Seed default for sitewide search-engine crawl/index when DB setting is missing.
/// Runtime value lives in SystemSettings key <c>Seo.AllowIndexing</c> (Admin → Cấu hình).
/// </summary>
public sealed class SeoOptions
{
    public const string SectionName = "Seo";

    /// <summary>Default when Admin chưa lưu. Staging/sslip.io nên false.</summary>
    public bool AllowIndexing { get; set; }
}
