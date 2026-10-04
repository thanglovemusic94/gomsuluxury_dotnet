using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shop;

public class LivestreamCheckoutDemoModel(AppDbContext db, TemporaryCartService carts) : PageModel
{
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var product = await db.Products.AsNoTracking()
            .WhereListedOnShop()
            .Where(item => item.Stock > 0)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync();

        if (product is null)
        {
            Error = "Chưa có sản phẩm còn hàng để tạo demo. Thêm/sửa tồn kho trong Admin trước.";
            return Page();
        }

        var (cart, error) = await carts.CreateAsync(
            productId: product.Id,
            phone: "0901234567",
            fbUserId: "demo-fb-user",
            sku: "SP01",
            quantity: 1,
            isDemo: true);

        if (cart is null)
        {
            Error = error ?? "Không tạo được giỏ tạm.";
            return Page();
        }

        TempData["Message"] = $"Đã tạo giỏ tạm demo cho «{product.Name}». Điền form và xác nhận để tạo đơn thật.";
        return Redirect(carts.CheckoutPath(cart.Token));
    }
}
