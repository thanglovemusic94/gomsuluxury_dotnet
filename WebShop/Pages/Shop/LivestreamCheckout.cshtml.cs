using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class LivestreamCheckoutModel(AppDbContext db, TemporaryCartService carts) : PageModel
{
    public TemporaryCart? Cart { get; private set; }

    public Product? Product { get; private set; }

    public string? OrderCode { get; private set; }

    public string? StateMessage { get; private set; }

    [BindProperty]
    public CheckoutInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string token)
    {
        OrderCode = TempData["OrderCode"] as string;
        Cart = await carts.GetByTokenAsync(token);
        Product = Cart?.Product;

        if (!string.IsNullOrEmpty(OrderCode))
            return Page();

        if (Cart is null)
        {
            StateMessage = "Link không hợp lệ hoặc đã bị xóa.";
            return Page();
        }

        if (Cart.Status == TemporaryCartStatuses.Ordered)
        {
            StateMessage = "Đơn trên link này đã được xác nhận trước đó.";
            if (Cart.OrderId is int orderId)
            {
                var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == orderId);
                OrderCode = order?.OrderCode;
            }

            return Page();
        }

        if (Cart.Status == TemporaryCartStatuses.Expired || Cart.ExpiresAt < DateTime.UtcNow)
        {
            StateMessage = "Link đã hết hạn. Hãy chốt lại trên livestream để nhận link mới.";
            return Page();
        }

        Input.CustomerPhone = Cart.CustomerPhone ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string token)
    {
        Cart = await carts.GetByTokenAsync(token);
        if (Cart is null)
        {
            StateMessage = "Link không hợp lệ hoặc đã bị xóa.";
            return Page();
        }

        Product = Cart.Product;
        if (Cart.Status == TemporaryCartStatuses.Ordered)
        {
            StateMessage = "Đơn trên link này đã được xác nhận trước đó.";
            return Page();
        }

        if (Cart.Status == TemporaryCartStatuses.Expired || Cart.ExpiresAt < DateTime.UtcNow)
        {
            StateMessage = "Link đã hết hạn. Hãy chốt lại trên livestream để nhận link mới.";
            return Page();
        }

        Input.CustomerName = TextHelper.Trimmed(Input.CustomerName);
        Input.CustomerPhone = TextHelper.Trimmed(Input.CustomerPhone);
        Input.ShippingAddress = TextHelper.Trimmed(Input.ShippingAddress);
        Input.PaymentMethod = TextHelper.Trimmed(Input.PaymentMethod);
        if (Input.PaymentMethod.Length == 0)
            Input.PaymentMethod = "COD";

        if (Input.CustomerName.Length is < 2 or > 150)
            ModelState.AddModelError(string.Empty, "Nhập tên người nhận.");
        if (Input.CustomerPhone.Length is < 8 or > 20)
            ModelState.AddModelError(string.Empty, "Nhập số điện thoại.");
        if (Input.ShippingAddress.Length is < 5 or > 500)
            ModelState.AddModelError(string.Empty, "Nhập địa chỉ giao hàng.");

        var product = await db.Products.FirstOrDefaultAsync(item => item.Id == Cart.ProductId && item.IsVisible);
        if (product is null)
            ModelState.AddModelError(string.Empty, "Sản phẩm không còn bán.");
        else if (product.Stock < Cart.Quantity)
            ModelState.AddModelError(string.Empty, $"Không đủ tồn kho: {product.Name}.");

        if (!ModelState.IsValid || product is null)
            return Page();

        var tracked = await db.TemporaryCarts.FirstAsync(item => item.Id == Cart.Id);
        if (tracked.Status != TemporaryCartStatuses.Pending)
        {
            StateMessage = "Đơn trên link này không còn hiệu lực.";
            return Page();
        }

        var sequence = await db.Orders.CountAsync() + 1;
        var code = $"DH-{DateTime.UtcNow.Year}-{sequence:000}";
        while (await db.Orders.AnyAsync(order => order.OrderCode == code))
        {
            sequence++;
            code = $"DH-{DateTime.UtcNow.Year}-{sequence:000}";
        }

        int? userId = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
            userId = parsed;

        var unitPrice = tracked.UnitPrice;
        var order = new Order
        {
            OrderCode = code,
            UserId = userId,
            Status = "Pending",
            PaymentStatus = "Unpaid",
            PaymentMethod = Input.PaymentMethod,
            CustomerName = Input.CustomerName,
            CustomerPhone = Input.CustomerPhone,
            ShippingAddress = Input.ShippingAddress,
            OrderNote = TextHelper.Clean(
                string.IsNullOrWhiteSpace(Input.OrderNote)
                    ? (tracked.IsDemo ? "[Demo livestream]" : "[Livestream]")
                    : $"{(tracked.IsDemo ? "[Demo livestream] " : "[Livestream] ")}{Input.OrderNote}")
        };

        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            Quantity = tracked.Quantity,
            UnitPrice = unitPrice,
            OriginalCostPrice = product.CostPrice
        });
        order.TotalAmount = unitPrice * tracked.Quantity;
        product.Stock -= tracked.Quantity;

        db.Orders.Add(order);
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        tracked.Status = TemporaryCartStatuses.Ordered;
        tracked.OrderId = order.Id;
        tracked.CustomerPhone = Input.CustomerPhone;
        await db.SaveChangesAsync();

        TempData["OrderCode"] = order.OrderCode;
        return RedirectToPage(new { token });
    }

    public class CheckoutInput
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string? OrderNote { get; set; }
        public string PaymentMethod { get; set; } = "COD";
    }
}
