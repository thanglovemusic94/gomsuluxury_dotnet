using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WebShop.Infrastructure;

public static class TextHelper
{
    public static string Slug(string? preferred, string fallback, bool keepSlash = false)
    {
        var source = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();
        source = source.Replace('đ', 'd').Replace('Đ', 'D');
        var normalized = source.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        var slug = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var pattern = keepSlash ? "[^a-z0-9/]+" : "[^a-z0-9]+";
        slug = Regex.Replace(slug, pattern, "-").Trim('-');
        if (keepSlash)
            slug = Regex.Replace(slug, "/{2,}", "/");
        return string.IsNullOrEmpty(slug) ? "muc" : slug;
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Trimmed(string? value) => value?.Trim() ?? string.Empty;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string? password, string? stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
            return false;

        var parts = stored.Split('.');
        if (parts.Length != 2)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
