using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using WebShop.Models;

namespace WebShop.Infrastructure;

/// <summary>Build Schema.org JSON-LD graphs from shop data (no manual JSON per product).</summary>
public static class SchemaOrg
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(object graph) => JsonSerializer.Serialize(graph, JsonOptions);

    public static Dictionary<string, object?> Organization(ShopSnapshot snap, HttpRequest request, string? logoUrl = null)
    {
        var sameAs = new List<string>();
        foreach (var url in new[] { snap.FacebookUrl, snap.TikTokUrl, snap.ZaloUrl, snap.YoutubeUrl })
        {
            if (!string.IsNullOrWhiteSpace(url))
                sameAs.Add(url.Trim());
        }

        var absLogo = SiteSocial.Absolute(request, MediaUrls.For(
            SiteSocial.PickImage(logoUrl, snap.LogoUrl, snap.ShareImageUrl), MediaSize.Medium));

        var org = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = string.IsNullOrWhiteSpace(snap.OrgType) ? "Organization" : snap.OrgType.Trim(),
            ["name"] = snap.SiteName,
            ["url"] = SiteSocial.Absolute(request, "/")
        };

        if (!string.IsNullOrWhiteSpace(absLogo))
            org["logo"] = absLogo;
        if (!string.IsNullOrWhiteSpace(snap.Hotline))
            org["telephone"] = snap.Hotline;
        if (!string.IsNullOrWhiteSpace(snap.Email))
            org["email"] = snap.Email;
        if (!string.IsNullOrWhiteSpace(snap.Address))
        {
            org["address"] = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = snap.Address,
                ["addressCountry"] = "VN"
            };
        }

        if (!string.IsNullOrWhiteSpace(snap.OpeningHours))
            org["openingHours"] = snap.OpeningHours.Trim();

        if (TryParsePair(snap.GeoCoordinates, out var lat, out var lng))
        {
            org["geo"] = new Dictionary<string, object?>
            {
                ["@type"] = "GeoCoordinates",
                ["latitude"] = lat,
                ["longitude"] = lng
            };
        }

        if (sameAs.Count > 0)
            org["sameAs"] = sameAs;

        return org;
    }

    /// <summary>
    /// Extract lat,lng from paste text (pair or Maps URL). Returns normalized "lat, lng" or empty.
    /// </summary>
    public static string NormalizePair(string? raw)
    {
        return TryParsePair(raw, out var lat, out var lng)
            ? FormattableString.Invariant($"{lat}, {lng}")
            : string.Empty;
    }

    public static bool TryParsePair(string? raw, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // Supports "21.02, 105.78" and Maps URLs containing the same pair.
        var match = System.Text.RegularExpressions.Regex.Match(
            raw.Trim(),
            @"(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)");
        if (!match.Success)
            return false;

        if (!double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lat))
            return false;
        if (!double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lng))
            return false;

        return lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
    }

    public static Dictionary<string, object?> WebSite(ShopSnapshot snap, HttpRequest request)
    {
        var siteUrl = SiteSocial.Absolute(request, "/");
        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = snap.SiteName,
            ["url"] = siteUrl,
            ["potentialAction"] = new Dictionary<string, object?>
            {
                ["@type"] = "SearchAction",
                ["target"] = SiteSocial.Absolute(request, "/san-pham?q={search_term_string}"),
                ["query-input"] = "required name=search_term_string"
            }
        };
    }

    public static Dictionary<string, object?> Product(
        HttpRequest request,
        Product product,
        IReadOnlyList<ProductReview> reviews,
        string? canonicalUrl)
    {
        var images = new List<string>();
        void AddImg(string? url)
        {
            var abs = SiteSocial.Absolute(request, MediaUrls.For(url, MediaSize.Large));
            if (string.IsNullOrWhiteSpace(abs)) return;
            if (!images.Contains(abs, StringComparer.OrdinalIgnoreCase))
                images.Add(abs);
        }

        AddImg(SiteSocial.PickImage(product.SeoImage, product.ImageUrl));
            foreach (var img in product.Images.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id))
                AddImg(img.ImageUrl);

        var price = product.DiscountPrice ?? product.Price;
        var availability = product.Stock > 0
            ? "https://schema.org/InStock"
            : "https://schema.org/OutOfStock";

        var graph = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = product.Name,
            ["sku"] = product.Slug,
            ["url"] = string.IsNullOrWhiteSpace(canonicalUrl)
                ? SiteSocial.Absolute(request, "/san-pham/" + product.Slug)
                : canonicalUrl
        };

        var desc = TextHelper.Clean(product.ShortDescription) ?? TextHelper.Clean(product.MetaDescription);
        if (!string.IsNullOrWhiteSpace(desc))
            graph["description"] = desc;
        if (images.Count == 1)
            graph["image"] = images[0];
        else if (images.Count > 1)
            graph["image"] = images;

        graph["offers"] = new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["url"] = graph["url"],
            ["priceCurrency"] = "VND",
            ["price"] = decimal.Round(price, 0, MidpointRounding.AwayFromZero),
            ["availability"] = availability,
            ["itemCondition"] = "https://schema.org/NewCondition"
        };

        if (reviews.Count > 0)
        {
            var avg = reviews.Average(item => item.Rating);
            graph["aggregateRating"] = new Dictionary<string, object?>
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = Math.Round(avg, 1),
                ["reviewCount"] = reviews.Count,
                ["bestRating"] = 5,
                ["worstRating"] = 1
            };
        }

        return graph;
    }

    public static Dictionary<string, object?> BlogPosting(
        HttpRequest request,
        Post post,
        string siteName,
        string? canonicalUrl)
    {
        var image = SiteSocial.Absolute(request,
            MediaUrls.For(SiteSocial.PickImage(post.SeoImage, post.ImageUrl), MediaSize.Large));
        var authorName = post.Author?.FullName;
        if (string.IsNullOrWhiteSpace(authorName))
            authorName = post.Author?.Username;
        if (string.IsNullOrWhiteSpace(authorName))
            authorName = siteName;

        var graph = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BlogPosting",
            ["headline"] = post.MetaTitle ?? post.Title,
            ["datePublished"] = post.CreatedAt.ToString("o"),
            ["dateModified"] = (post.UpdatedAt ?? post.CreatedAt).ToString("o"),
            ["author"] = new Dictionary<string, object?>
            {
                ["@type"] = "Person",
                ["name"] = authorName
            },
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = siteName
            },
            ["mainEntityOfPage"] = string.IsNullOrWhiteSpace(canonicalUrl)
                ? SiteSocial.Absolute(request, "/blog/" + post.Slug)
                : canonicalUrl,
            ["url"] = string.IsNullOrWhiteSpace(canonicalUrl)
                ? SiteSocial.Absolute(request, "/blog/" + post.Slug)
                : canonicalUrl
        };

        var desc = TextHelper.Clean(post.MetaDescription) ?? TextHelper.Clean(post.Summary);
        if (!string.IsNullOrWhiteSpace(desc))
            graph["description"] = desc;
        if (!string.IsNullOrWhiteSpace(image))
            graph["image"] = image;

        return graph;
    }
}
