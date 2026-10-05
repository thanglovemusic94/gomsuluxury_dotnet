using System.Text.RegularExpressions;

namespace WebShop.Infrastructure;

/// <summary>Parse ShortDescription thành cặp nhãn–giá trị (dòng hoặc dính liền).</summary>
public static partial class ProductSpecs
{
    /// <summary>Nhãn phổ biến — khớp theo độ dài giảm dần để tránh cắt nhầm.</summary>
    private static readonly string[] KnownLabels =
    [
        "Tên sản phẩm",
        "Dòng sản phẩm",
        "Chất liệu & Men",
        "Chất liệu và men",
        "Kỹ thuật men",
        "Tính chất men",
        "Nguồn gốc",
        "Xuất xứ",
        "Nghệ nhân",
        "Thương hiệu",
        "Chất liệu",
        "Đặc điểm",
        "Kích thước",
        "Trọng lượng",
        "Màu sắc",
        "Bảo hành",
        "Dáng"
    ];

    private static readonly HashSet<string> SkipKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tên sản phẩm",
        "Ten san pham"
    };

    private static readonly Regex KnownLabelRegex = BuildKnownLabelRegex();

    public static IReadOnlyList<KeyValuePair<string, string>> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var body = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        body = HeaderPrefix().Replace(body, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(body))
            return [];

        if (body.Contains('\n'))
        {
            var fromLines = ParseLines(body);
            if (fromLines.Count > 0)
                return fromLines;
        }

        return ParseKnownLabels(body);
    }

    private static List<KeyValuePair<string, string>> ParseLines(string body)
    {
        var rows = new List<KeyValuePair<string, string>>();
        foreach (var raw in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = BulletPrefix().Replace(raw, string.Empty).Trim();
            var idx = line.IndexOf(':');
            if (idx <= 0 || idx > 48)
                continue;
            Add(rows, line[..idx], line[(idx + 1)..]);
        }

        return rows;
    }

    private static List<KeyValuePair<string, string>> ParseKnownLabels(string body)
    {
        var rows = new List<KeyValuePair<string, string>>();
        var matches = KnownLabelRegex.Matches(body);
        if (matches.Count == 0)
            return rows;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var key = match.Groups[1].Value.Trim();
            var start = match.Index + match.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            if (end <= start)
                continue;
            Add(rows, key, TrimValue(body[start..end]));
        }

        return rows;
    }

    private static string TrimValue(string value)
    {
        value = value.Trim().TrimEnd(';', '·', '|').Trim();
        if (value.Length <= 180)
            return value;

        var dash = value.IndexOf(" – ", StringComparison.Ordinal);
        if (dash > 40)
            return value[..dash].Trim();

        var dot = value.IndexOf(". ", StringComparison.Ordinal);
        if (dot > 40)
            return value[..dot].Trim();

        return value[..180].TrimEnd() + "…";
    }

    private static void Add(List<KeyValuePair<string, string>> rows, string key, string value)
    {
        key = key.Trim().TrimStart('•', '-', '*', '·').Trim();
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return;
        if (SkipKeys.Contains(key))
            return;
        rows.Add(new KeyValuePair<string, string>(key, value));
    }

    private static Regex BuildKnownLabelRegex()
    {
        var alt = string.Join("|",
            KnownLabels
                .OrderByDescending(label => label.Length)
                .Select(Regex.Escape));
        return new Regex(
            $@"(?:^|[\s\|•\-\*·])({alt})\s*:\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    [GeneratedRegex(@"^\s*THÔNG TIN SẢN PHẨM\s*[:\-]?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeaderPrefix();

    [GeneratedRegex(@"^\s*[•\-\*·]\s*")]
    private static partial Regex BulletPrefix();
}
