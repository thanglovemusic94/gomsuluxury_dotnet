namespace WebShop.Infrastructure;

/// <summary>Nguồn đánh giá — Web (form) / Livestream / Inbox (Admin curate).</summary>
public static class ProductReviewSources
{
    public const string Web = "Web";
    public const string Livestream = "Livestream";
    public const string Inbox = "Inbox";

    public static readonly (string Value, string Label)[] AdminChoices =
    [
        (Livestream, "Livestream"),
        (Inbox, "Inbox / Zalo"),
        (Web, "Web")
    ];

    public static string Normalize(string? source)
    {
        var value = (source ?? string.Empty).Trim();
        if (string.Equals(value, Livestream, StringComparison.OrdinalIgnoreCase))
            return Livestream;
        if (string.Equals(value, Inbox, StringComparison.OrdinalIgnoreCase))
            return Inbox;
        return Web;
    }

    public static string? BadgeLabel(string? source) => Normalize(source) switch
    {
        Livestream => "Đã mua tại Livestream",
        Inbox => "Khách gửi ảnh / inbox",
        _ => null
    };
}
