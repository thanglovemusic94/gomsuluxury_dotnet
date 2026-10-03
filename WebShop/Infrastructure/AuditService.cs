using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public sealed class AuditService(AppDbContext db, IHttpContextAccessor http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task LogAsync(
        string action,
        string entityType,
        int entityId,
        string? entityTitle,
        string? summary,
        object? details = null,
        CancellationToken ct = default)
    {
        var user = http.HttpContext?.User;
        int? userId = null;
        if (int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
            userId = parsed;

        var username = user?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            username = "system";

        string? detailsJson = null;
        if (details is string text)
            detailsJson = text;
        else if (details is not null)
            detailsJson = JsonSerializer.Serialize(details, JsonOptions);

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Username = username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityTitle = TextHelper.Clean(entityTitle),
            Summary = TextHelper.Clean(summary),
            Details = detailsJson,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public static List<string> Diff(Dictionary<string, string?> before, Dictionary<string, string?> after)
    {
        var lines = new List<string>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            before.TryGetValue(key, out var oldValue);
            after.TryGetValue(key, out var newValue);
            oldValue ??= string.Empty;
            newValue ??= string.Empty;
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                continue;
            lines.Add($"{key}: {Trim(oldValue)} → {Trim(newValue)}");
        }

        return lines;
    }

    private static string Trim(string value) =>
        value.Length <= 120 ? value : value[..117] + "...";
}
