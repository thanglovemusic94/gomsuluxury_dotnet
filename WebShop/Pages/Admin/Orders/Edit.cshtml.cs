using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Orders;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public OrderInput Input { get; set; } = new();

    public IList<OrderItem> Items { get; private set; } = [];

    public decimal CostTotal => Items.Sum(item => item.OriginalCostPrice * item.Quantity);

    public decimal Revenue => Items.Sum(item => item.UnitPrice * item.Quantity);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (order is null)
            return NotFound();

        Input = OrderInput.From(order);
        Items = await db.OrderItems.AsNoTracking()
            .Include(item => item.Product)
            .Where(item => item.OrderId == id)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!CatalogRules.OrderStatuses.Contains(Input.Status))
            ModelState.AddModelError("Input.Status", "Trạng thái không hợp lệ.");
        if (!CatalogRules.PaymentStatuses.Contains(Input.PaymentStatus))
            ModelState.AddModelError("Input.PaymentStatus", "Trạng thái thanh toán không hợp lệ.");
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
        if (Input.OrderNote?.Length > 500)
            ModelState.AddModelError("Input.OrderNote", "Ghi chú tối đa 500 ký tự.");

        var order = await db.Orders.Include(item => item.Items).ThenInclude(item => item.Product).FirstOrDefaultAsync(item => item.Id == Input.Id);
        if (order is null)
            return NotFound();

        Items = order.Items;
        if (!ModelState.IsValid)
            return Page();

        var wasCancelled = order.Status == "Cancelled";
        var nowCancelled = Input.Status == "Cancelled";
        if (wasCancelled != nowCancelled)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            if (nowCancelled)
            {
                foreach (var line in order.Items)
                    await StockInventory.RestoreAsync(db, line.ProductId, line.Quantity);
            }
            else
            {
                var products = await db.Products.AsNoTracking()
                    .Where(product => order.Items.Select(item => item.ProductId).Contains(product.Id))
                    .ToDictionaryAsync(product => product.Id);
                var stockLines = order.Items
                    .Select(line =>
                    {
                        products.TryGetValue(line.ProductId, out var product);
                        return (line.ProductId, line.Quantity, product?.Name ?? $"#{line.ProductId}");
                    })
                    .ToList();
                var (stockOk, stockError) = await StockInventory.TryDecrementManyAsync(db, stockLines);
                if (!stockOk)
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError(string.Empty, stockError ?? "Không đủ tồn kho để mở lại đơn.");
                    return Page();
                }
            }

            order.Status = Input.Status;
            order.PaymentStatus = Input.PaymentStatus;
            order.PaymentMethod = Input.PaymentMethod.Trim();
            order.CustomerName = Input.CustomerName.Trim();
            order.CustomerPhone = Input.CustomerPhone.Trim();
            order.ShippingAddress = Input.ShippingAddress.Trim();
            order.OrderNote = TextHelper.Clean(Input.OrderNote);
            if (!await DbSave.TrySaveAsync(db, ModelState))
            {
                await tx.RollbackAsync();
                return Page();
            }

            await tx.CommitAsync();
            TempData["Message"] = "Đã cập nhật đơn hàng.";
            return RedirectToPage(new { id = order.Id });
        }

        order.Status = Input.Status;
        order.PaymentStatus = Input.PaymentStatus;
        order.PaymentMethod = Input.PaymentMethod.Trim();
        order.CustomerName = Input.CustomerName.Trim();
        order.CustomerPhone = Input.CustomerPhone.Trim();
        order.ShippingAddress = Input.ShippingAddress.Trim();
        order.OrderNote = TextHelper.Clean(Input.OrderNote);
        if (!await DbSave.TrySaveAsync(db, ModelState))
            return Page();

        TempData["Message"] = "Đã cập nhật đơn hàng.";
        return RedirectToPage(new { id = order.Id });
    }

    public class OrderInput
    {
        public int Id { get; set; }

        public string OrderCode { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public string PaymentMethod { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = "Unpaid";

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerPhone { get; set; } = string.Empty;

        public string ShippingAddress { get; set; } = string.Empty;

        public string? OrderNote { get; set; }

        public string? Source { get; set; }

        public decimal TotalAmount { get; set; }

        public static OrderInput From(Order order) => new()
        {
            Id = order.Id,
            OrderCode = order.OrderCode,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            ShippingAddress = order.ShippingAddress,
            OrderNote = order.OrderNote,
            Source = order.Source,
            TotalAmount = order.TotalAmount
        };
    }
}
