using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Orders;

public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public CreateInput Input { get; set; } = new();

    public IList<Product> Products { get; private set; } = [];

    public IList<User> Customers { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        Input.CustomerName = TextHelper.Trimmed(Input.CustomerName);
        Input.CustomerPhone = TextHelper.Trimmed(Input.CustomerPhone);
        Input.ShippingAddress = TextHelper.Trimmed(Input.ShippingAddress);
        Input.PaymentMethod = TextHelper.Trimmed(Input.PaymentMethod);
        if (Input.CustomerName.Length is < 1 or > 100)
            ModelState.AddModelError("Input.CustomerName", "Tên khách từ 1 đến 100 ký tự.");
        if (Input.CustomerPhone.Length is < 1 or > 15)
            ModelState.AddModelError("Input.CustomerPhone", "Số điện thoại tối đa 15 ký tự.");
        if (Input.ShippingAddress.Length is < 1 or > 500)
            ModelState.AddModelError("Input.ShippingAddress", "Địa chỉ tối đa 500 ký tự.");
        if (Input.PaymentMethod.Length is < 1 or > 50)
            ModelState.AddModelError("Input.PaymentMethod", "Nhập phương thức thanh toán.");
        if (Input.OrderNote?.Length > 500)
            ModelState.AddModelError("Input.OrderNote", "Ghi chú tối đa 500 ký tự.");

        var lines = Input.Items
            .Where(item => item.ProductId > 0 && item.Quantity > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToList();
        if (lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Chọn ít nhất một sản phẩm.");

        var products = await db.Products.Where(product => lines.Select(line => line.ProductId).Contains(product.Id)).ToListAsync();
        if (products.Count != lines.Count)
            ModelState.AddModelError(string.Empty, "Có sản phẩm không còn tồn tại.");

        foreach (var line in lines)
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

        var order = new Order
        {
            OrderCode = code,
            UserId = Input.UserId,
            Status = "Pending",
            PaymentStatus = "Unpaid",
            PaymentMethod = Input.PaymentMethod,
            CustomerName = Input.CustomerName,
            CustomerPhone = Input.CustomerPhone,
            ShippingAddress = Input.ShippingAddress,
            OrderNote = TextHelper.Clean(Input.OrderNote)
        };

        foreach (var line in lines)
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
        var stockLines = lines
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
            await LoadAsync();
            return Page();
        }

        db.Orders.Add(order);
        if (!await DbSave.TrySaveAsync(db, ModelState))
        {
            await tx.RollbackAsync();
            await LoadAsync();
            return Page();
        }

        await tx.CommitAsync();
        TempData["Message"] = $"Đã tạo đơn {order.OrderCode}.";
        return RedirectToPage("Edit", new { id = order.Id });
    }

    private async Task LoadAsync()
    {
        Products = await db.Products.AsNoTracking().Where(product => product.IsVisible).OrderBy(product => product.Name).ToListAsync();
        Customers = await db.Users.AsNoTracking().OrderBy(user => user.Username).ToListAsync();
        if (Input.Items.Count == 0)
            Input.Items = [new LineInput(), new LineInput(), new LineInput()];
    }

    public class CreateInput
    {
        public int? UserId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerPhone { get; set; } = string.Empty;

        public string ShippingAddress { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = "COD";

        public string? OrderNote { get; set; }

        public List<LineInput> Items { get; set; } = [new(), new(), new()];
    }

    public class LineInput
    {
        public int ProductId { get; set; }

        public int Quantity { get; set; } = 1;
    }
}
