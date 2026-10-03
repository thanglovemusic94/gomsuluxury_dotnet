using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

public static class DbSave
{
    public static async Task<bool> TrySaveAsync(AppDbContext db, ModelStateDictionary modelState)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            modelState.AddModelError(string.Empty, Message(ex));
            return false;
        }
    }

    public static async Task<string?> TryDeleteAsync(AppDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException ex)
        {
            return Message(ex);
        }
    }

    private static string Message(DbUpdateException ex)
    {
        var text = ex.InnerException?.Message ?? ex.Message;
        return text.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            ? "Dữ liệu bị trùng. Kiểm tra lại slug, email, tên hoặc mã."
            : "Không thực hiện được vì dữ liệu còn được dùng ở chỗ khác.";
    }
}
