using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebShop.Infrastructure;

public static class ModelMessages
{
    public static IReadOnlyList<string> Visible(ModelStateDictionary modelState)
    {
        var messages = new List<string>();
        foreach (var entry in modelState)
        {
            var custom = new List<string>();
            var framework = false;
            foreach (var error in entry.Value?.Errors ?? [])
            {
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? error.Exception?.Message
                    : error.ErrorMessage;
                if (string.IsNullOrWhiteSpace(message))
                    continue;

                if (IsFramework(message))
                    framework = true;
                else
                    custom.Add(message);
            }

            if (custom.Count > 0)
                messages.AddRange(custom);
            else if (framework)
                messages.Add(Friendly(entry.Key));
        }

        return messages;
    }

    private static bool IsFramework(string message) =>
        message.EndsWith(" field is required.", StringComparison.Ordinal)
        || message.Contains("is invalid", StringComparison.OrdinalIgnoreCase)
        || message.Contains("must be a number", StringComparison.OrdinalIgnoreCase)
        || message.StartsWith("The value ", StringComparison.Ordinal);

    private static string Friendly(string key)
    {
        var name = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
        return name switch
        {
            "Price" => "Nhập giá bán hợp lệ.",
            "CostPrice" => "Nhập giá vốn hợp lệ.",
            "DiscountPrice" => "Giá khuyến mãi không hợp lệ.",
            "Stock" => "Nhập tồn kho hợp lệ.",
            "SeoScore" => "Điểm SEO phải là số.",
            "CategoryId" => "Chọn danh mục.",
            "Quantity" or "DisplayOrder" or "Rating" => "Nhập số hợp lệ.",
            "Name" or "Title" or "Username" => "Nhập tên.",
            "ImageUrl" => "Nhập URL ảnh.",
            "Description" or "Content" => "Nhập nội dung.",
            _ => "Dữ liệu không hợp lệ."
        };
    }
}
