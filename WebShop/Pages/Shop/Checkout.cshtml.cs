using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class CheckoutModel(AppDbContext db, CartService cart) : PageModel
{
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];

    public string? OrderCode { get; private set; }

    [BindProperty]
    public CheckoutInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        OrderCode = TempData["OrderCode"] as string;
        Lines = cart.Get();
        if (Lines.Count == 0 && string.IsNullOrEmpty(OrderCode))
            return RedirectToPage("/Shop/Cart");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Lines = cart.Get();
        Input.CustomerName = TextHelper.Trimmed(Input.CustomerName);
        Input.CustomerPhone = TextHelper.Trimmed(Input.CustomerPhone);
        Input.ShippingAddress = TextHelper.Trimmed(Input.ShippingAddress);
        Input.PaymentMethod = TextHelper.Trimmed(Input.PaymentMethod);
        if (Input.PaymentMethod.Length == 0)
            Input.PaymentMethod = "COD";

        if (Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Giỏ hàng đang trống.");
        if (Input.CustomerName.Length is < 2 or > 150)
            ModelState.AddModelError(string.Empty, "Nhập tên người nhận.");
        if (Input.CustomerPhone.Length is < 8 or > 20)
            ModelState.AddModelError(string.Empty, "Nhập số điện thoại.");
        if (Input.ShippingAddress.Length is < 5 or > 500)
            ModelState.AddModelError(string.Empty, "Nhập địa chỉ giao hàng.");

        var ids = Lines.Select(line => line.ProductId).ToList();
        var products = await db.Products.Where(product => ids.Contains(product.Id) && product.IsVisible).ToListAsync();
        if (products.Count != Lines.Count)
            ModelState.AddModelError(string.Empty, "Một sản phẩm không còn bán.");

        foreach (var line in Lines)
        {
            var product = products.FirstOrDefault(item => item.Id == line.ProductId);
            if (product is not null && product.Stock < line.Quantity)
                ModelState.AddModelError(string.Empty, $"Không đủ tồn kho: {product.Name}.");
        }

        if (!ModelState.IsValid)
            return Page();

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
            OrderNote = TextHelper.Clean(Input.OrderNote)
        };

        foreach (var line in Lines)
        {
            var product = products.First(item => item.Id == line.ProductId);
            var unitPrice = product.DiscountPrice ?? product.Price;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                OriginalCostPrice = product.CostPrice
            });
            order.TotalAmount += unitPrice * line.Quantity;
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var stockLines = Lines
            .Select(line =>
            {
                var product = products.First(item => item.Id == line.ProductId);
                return (product.Id, line.Quantity, product.Name);
            })
            .ToList();
        var (stockOk, stockError) = await StockInventory.TryDecrementManyAsync(db, stockLines);
        if (!stockOk)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, stockError ?? "Không đủ tồn kho.");
            return Page();
        }

        db.Orders.Add(order);
        if (!await DbSave.TrySaveAsync(db, ModelState))
        {
            await tx.RollbackAsync();
            return Page();
        }

        await tx.CommitAsync();
        cart.Clear();
        TempData["OrderCode"] = order.OrderCode;
        return RedirectToPage();
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
