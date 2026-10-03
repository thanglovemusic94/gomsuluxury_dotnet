using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace WebShop.Infrastructure;

public sealed class GeminiSeoService(IHttpClientFactory httpClientFactory, IOptions<GeminiOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<SeoAiResult> AnalyzeProductAsync(SeoAiRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SeoAiResult.Fail("Chưa cấu hình Gemini:ApiKey (appsettings.Local.json hoặc biến môi trường Gemini__ApiKey).");

        var models = ResolveModels();
        var userPrompt = BuildUserPrompt(request);
        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = """
                            Bạn là chuyên gia SEO tiếng Việt cho website thương mại điện tử.
                            Đánh giá SEO on-page và đề xuất cải thiện. Trả lời DUY NHẤT một JSON hợp lệ, không markdown.
                            Schema:
                            {
                              "score": 0-100,
                              "summary": "tóm tắt ngắn",
                              "issues": ["vấn đề 1", "..."],
                              "suggestions": ["gợi ý 1", "..."],
                              "proposedMetaTitle": "tối đa 60 ký tự, có thể rỗng nếu không đề xuất",
                              "proposedMetaDescription": "tối đa 155 ký tự",
                              "proposedFocusKeyword": "từ khóa chính"
                            }
                            Tiêu chí: title/slug/meta, từ khóa, mô tả, độ dài, ảnh SEO, tính hấp dẫn và rõ ràng cho người Việt.
                            """
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.35,
                responseMimeType = "application/json"
            }
        };

        var client = httpClientFactory.CreateClient("gemini");
        string? lastError = null;

        foreach (var model in models)
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var url =
                    $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(options.Value.ApiKey.Trim())}";

                using var response = await client.PostAsJsonAsync(url, payload, ct);
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    var text = ExtractModelText(raw);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        lastError = $"Model {model}: Gemini không trả nội dung.";
                        break;
                    }

                    var parsed = ParseResult(text);
                    if (parsed is null)
                    {
                        lastError = $"Model {model}: Không đọc được JSON từ Gemini.";
                        break;
                    }

                    if (!string.Equals(model, models[0], StringComparison.OrdinalIgnoreCase))
                        parsed = parsed with { Summary = $"[{model}] {parsed.Summary}".Trim() };
                    return parsed;
                }

                var apiError = ExtractApiError(raw) ?? $"Gemini lỗi HTTP {(int)response.StatusCode}.";
                lastError = $"{model}: {apiError}";

                if (!IsRetryable(response.StatusCode, apiError) || attempt == 2)
                    break;

                await Task.Delay(1200 * attempt, ct);
            }
        }

        return SeoAiResult.Fail(
            "Gemini đang quá tải hoặc model tạm không dùng được. Thử lại sau vài phút.\n" +
            (lastError ?? string.Empty));
    }

    private IReadOnlyList<string> ResolveModels()
    {
        var list = new List<string>();
        var primary = string.IsNullOrWhiteSpace(options.Value.Model) ? "gemini-3.8-flash" : options.Value.Model.Trim();
        list.Add(primary);
        foreach (var item in options.Value.FallbackModels ?? [])
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            var name = item.Trim();
            if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
                list.Add(name);
        }

        return list;
    }

    private static bool IsRetryable(System.Net.HttpStatusCode status, string message)
    {
        if (status is System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.InternalServerError)
            return true;

        var m = message.ToLowerInvariant();
        return m.Contains("high demand")
               || m.Contains("try again later")
               || m.Contains("resource exhausted")
               || m.Contains("unavailable")
               || m.Contains("overloaded")
               || m.Contains("no longer available")
               || m.Contains("not found");
    }

    private static string BuildUserPrompt(SeoAiRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Đánh giá SEO cho sản phẩm sau:");
        sb.AppendLine($"Loại: {request.EntityType}");
        sb.AppendLine($"Tên: {request.Name}");
        sb.AppendLine($"Slug/URL path: {request.Slug}");
        sb.AppendLine($"Từ khóa focus: {NullDash(request.FocusKeyword)}");
        sb.AppendLine($"Meta title: {NullDash(request.MetaTitle)}");
        sb.AppendLine($"Meta description: {NullDash(request.MetaDescription)}");
        sb.AppendLine($"Ảnh đại diện: {NullDash(request.ImageUrl)}");
        sb.AppendLine($"Ảnh SEO: {NullDash(request.SeoImage)}");
        sb.AppendLine($"Mô tả ngắn: {NullDash(request.ShortDescription)}");
        sb.AppendLine("Mô tả (đã bỏ HTML):");
        sb.AppendLine(Truncate(StripHtml(request.Description), 2500));
        return sb.ToString();
    }

    private static string? ExtractModelText(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return null;
            var content = candidates[0].GetProperty("content");
            if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                return null;
            return parts[0].GetProperty("text").GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractApiError(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static SeoAiResult? ParseResult(string text)
    {
        var json = text.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            json = Regex.Replace(json, "^```(?:json)?\\s*", "", RegexOptions.IgnoreCase);
            json = Regex.Replace(json, "\\s*```$", "");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<SeoAiDto>(json, JsonOptions);
            if (dto is null) return null;
            var score = Math.Clamp(dto.Score, 0, 100);
            return new SeoAiResult
            {
                Ok = true,
                Score = score,
                Summary = dto.Summary?.Trim() ?? string.Empty,
                Issues = dto.Issues?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Take(12).ToList() ?? [],
                Suggestions = dto.Suggestions?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Take(12).ToList() ?? [],
                ProposedMetaTitle = CleanLen(dto.ProposedMetaTitle, 100),
                ProposedMetaDescription = CleanLen(dto.ProposedMetaDescription, 255),
                ProposedFocusKeyword = CleanLen(dto.ProposedFocusKeyword, 100)
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? CleanLen(string? value, int max)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= max ? value : value[..max];
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, "\\s+", " ").Trim();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static string NullDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(trống)" : value.Trim();

    private sealed class SeoAiDto
    {
        public int Score { get; set; }
        public string? Summary { get; set; }
        public List<string>? Issues { get; set; }
        public List<string>? Suggestions { get; set; }
        public string? ProposedMetaTitle { get; set; }
        public string? ProposedMetaDescription { get; set; }
        public string? ProposedFocusKeyword { get; set; }
    }
}

public sealed class SeoAiRequest
{
    public string EntityType { get; init; } = "Product";
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? FocusKeyword { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? ImageUrl { get; init; }
    public string? SeoImage { get; init; }
    public string? ShortDescription { get; init; }
    public string? Description { get; init; }
}

public sealed record SeoAiResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public int Score { get; init; }
    public string Summary { get; init; } = string.Empty;
    public List<string> Issues { get; init; } = [];
    public List<string> Suggestions { get; init; } = [];
    public string? ProposedMetaTitle { get; init; }
    public string? ProposedMetaDescription { get; init; }
    public string? ProposedFocusKeyword { get; init; }

    public static SeoAiResult Fail(string error) => new() { Ok = false, Error = error };
}
