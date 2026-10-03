namespace WebShop.Infrastructure;

public static class CatalogRules
{
    public static readonly string[] CategoryTypes = ["Product", "Blog"];

    public static readonly string[] MenuPositions = ["Header", "Footer"];

    public static readonly string[] OrderStatuses = ["Pending", "Confirmed", "Shipping", "Completed", "Cancelled"];

    public static readonly string[] PaymentStatuses = ["Unpaid", "Paid"];
}
