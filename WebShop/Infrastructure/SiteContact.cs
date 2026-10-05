using System.Text;

namespace WebShop.Infrastructure;

public static class SiteContact
{
    public static string Digits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;
        var sb = new StringBuilder(phone.Length);
        foreach (var ch in phone)
        {
            if (char.IsDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    public static string? TelHref(string? hotline)
    {
        var digits = Digits(hotline);
        return digits.Length >= 8 ? "tel:" + digits : null;
    }

    public static string? ZaloHref(string? zaloUrl, string? hotline)
    {
        var url = TextHelper.Clean(zaloUrl);
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        var digits = Digits(hotline);
        return digits.Length >= 8 ? "https://zalo.me/" + digits : null;
    }

    public static string? FacebookHref(string? facebookUrl) => TextHelper.Clean(facebookUrl);

    public static string DisplayPhone(string? hotline)
    {
        var trimmed = TextHelper.Trimmed(hotline);
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        var digits = Digits(trimmed);
        if (digits.Length == 10)
            return $"{digits[..4]}.{digits[4..7]}.{digits[7..]}";
        if (digits.Length == 11)
            return $"{digits[..4]}.{digits[4..7]}.{digits[7..]}";

        return trimmed;
    }
}
