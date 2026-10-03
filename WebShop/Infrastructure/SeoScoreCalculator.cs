using System.Text.RegularExpressions;

namespace WebShop.Infrastructure;

/// <summary>
/// On-page SEO score 0–100 from fixed checklist (Yoast-style). Not user-editable.
/// </summary>
public static class SeoScoreCalculator
{
    public sealed record Request(
        string? Title,
        string? Slug,
        string? FocusKeyword,
        string? MetaTitle,
        string? MetaDescription,
        string? SeoImage,
        string? ImageUrl,
        string? ShortText,
        string? HtmlBody,
        IReadOnlyList<string>? ImageAlts = null);

    public sealed record BreakdownItem(string Key, string Label, int Points, bool Earned);

    public sealed record Result(int Score, IReadOnlyList<BreakdownItem> Items);

    public static int Compute(Request request) => Evaluate(request).Score;

    public static Result Evaluate(Request request)
    {
        var keywords = ParseKeywords(request.FocusKeyword);
        var kw = keywords.Count > 0 ? keywords[0] : string.Empty;
        var secondaries = keywords.Skip(1).ToList();
        var titleShown = First(request.MetaTitle, request.Title);
        var descShown = First(request.MetaDescription, request.ShortText, Plain(request.HtmlBody));
        var slug = Norm(request.Slug).Replace('-', ' ');
        var bodyPlain = Plain(request.HtmlBody);
        var firstWords = FirstWords(bodyPlain, 100);
        var words = CountWords(bodyPlain);
        var hasImage = !string.IsNullOrWhiteSpace(request.SeoImage)
                       || !string.IsNullOrWhiteSpace(request.ImageUrl);
        var altHit = !string.IsNullOrEmpty(kw)
                     && request.ImageAlts?.Any(alt => Contains(alt, kw)) == true;
        var internalLink = HasInternalLink(request.HtmlBody);

        var titleLen = (request.MetaTitle ?? string.Empty).Trim().Length;
        if (titleLen == 0)
            titleLen = (request.Title ?? string.Empty).Trim().Length;
        var descLen = (request.MetaDescription ?? string.Empty).Trim().Length;
        if (descLen == 0)
            descLen = Math.Min(160, (request.ShortText ?? string.Empty).Trim().Length);

        var secondaryHits = secondaries.Count(sec =>
            Contains(titleShown, sec) || Contains(descShown, sec) || Contains(slug, sec));
        var secondaryBonus = Math.Min(6, secondaryHits * 2);

        var items = new List<BreakdownItem>
        {
            Item("kw_title", "Từ khóa trong tiêu đề", 15, !string.IsNullOrEmpty(kw) && Contains(titleShown, kw)),
            Item("kw_desc", "Từ khóa trong mô tả", 10, !string.IsNullOrEmpty(kw) && Contains(descShown, kw)),
            Item("kw_slug", "Từ khóa trong slug/URL", 10, !string.IsNullOrEmpty(kw) && Contains(slug, kw)),
            Item("kw_lead", "Từ khóa trong ~100 từ đầu nội dung", 10, !string.IsNullOrEmpty(kw) && Contains(firstWords, kw)),
            Item("kw_heading", "Từ khóa trong H2/H3", 10, !string.IsNullOrEmpty(kw) && HasKeywordInHeadings(request.HtmlBody, kw)),
            Item("len_title", "Meta title 50–60 ký tự", 10, titleLen is >= 50 and <= 60),
            Item("len_desc", "Meta description 110–160 ký tự", 10, descLen is >= 110 and <= 160),
            Item("len_body", "Độ dài nội dung (≥600 / ≥1200 từ)", 15, words >= 600),
            Item("img", "Có ảnh SEO hoặc ảnh đại diện", 5, hasImage),
            Item("img_alt", "Alt ảnh chứa từ khóa chính", 5, altHit),
            Item("int_link", "Có liên kết nội bộ trong nội dung", 10, internalLink),
            Item("kw_sec", "Từ khóa phụ xuất hiện (tối đa +6)", 6, secondaryBonus > 0)
        };

        // Variable points for body length (10 or 15)
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Key != "len_body") continue;
            var pts = words >= 1200 ? 15 : words >= 600 ? 10 : 0;
            items[i] = items[i] with { Points = 15, Earned = pts > 0 };
            break;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Key != "kw_sec") continue;
            items[i] = items[i] with { Points = 6, Earned = secondaryBonus > 0 };
            break;
        }

        var score = 0;
        foreach (var item in items)
        {
            if (!item.Earned) continue;
            score += item.Key switch
            {
                "len_body" => words >= 1200 ? 15 : 10,
                "kw_sec" => secondaryBonus,
                _ => item.Points
            };
        }

        score = Math.Min(100, score);
        return new Result(score, items);
    }

    /// <summary>Split "a, b; c" → distinct keywords (max 5).</summary>
    public static IReadOnlyList<string> ParseKeywords(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Norm)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    public static string PrimaryKeyword(string? raw) =>
        ParseKeywords(raw).FirstOrDefault() ?? string.Empty;

    public static string JoinKeywords(IEnumerable<string> keywords) =>
        string.Join(", ", keywords.Select(Norm).Where(item => item.Length > 0).Take(5));

    private static BreakdownItem Item(string key, string label, int points, bool earned) =>
        new(key, label, points, earned);

    private static string First(params string?[] values)
    {
        foreach (var v in values)
        {
            var t = (v ?? string.Empty).Trim();
            if (t.Length > 0)
                return t;
        }

        return string.Empty;
    }

    private static string Norm(string? value) => (value ?? string.Empty).Trim();

    private static bool Contains(string hay, string kw) =>
        !string.IsNullOrEmpty(kw)
        && hay.Contains(kw, StringComparison.OrdinalIgnoreCase);

    private static string Plain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
        var text = Regex.Replace(html, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string FirstWords(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
            return string.Empty;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Take(count));
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static bool HasKeywordInHeadings(string? html, string kw)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrEmpty(kw))
            return false;
        foreach (Match m in Regex.Matches(html, @"<h[23]\b[^>]*>([\s\S]*?)</h[23]>", RegexOptions.IgnoreCase))
        {
            var inner = Plain(m.Groups[1].Value);
            if (Contains(inner, kw))
                return true;
        }

        return false;
    }

    private static bool HasInternalLink(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;
        foreach (Match m in Regex.Matches(html, @"<a\b[^>]*\bhref\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase))
        {
            var href = m.Groups[1].Value.Trim();
            if (href.StartsWith('#') || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (href.StartsWith('/') && !href.StartsWith("//"))
                return true;
            if (href.Contains("://", StringComparison.Ordinal) &&
                (href.Contains("/san-pham/", StringComparison.OrdinalIgnoreCase)
                 || href.Contains("/blog/", StringComparison.OrdinalIgnoreCase)
                 || href.Contains("/danh-muc/", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }
}
