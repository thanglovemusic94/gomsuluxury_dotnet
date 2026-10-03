using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Admin.Orders;

public class IndexModel(AppDbContext db) : PagedAdminPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PaymentStatus { get; set; }

    public IList<Order> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = Filtered();
        ApplyPagingTotals(await query.CountAsync());
        Items = await query
            .OrderByDescending(order => order.OrderDate)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public string ListUrl(int page, int size) =>
        AdminPaging.BuildUrl("/Admin/Orders", page, size,
            ("Q", Q),
            ("Status", Status),
            ("PaymentStatus", PaymentStatus));

    private IQueryable<Order> Filtered()
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            query = query.Where(item =>
                item.OrderCode.Contains(term) ||
                item.CustomerName.Contains(term) ||
                item.CustomerPhone.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(Status) && CatalogRules.OrderStatuses.Contains(Status))
            query = query.Where(item => item.Status == Status);

        if (!string.IsNullOrWhiteSpace(PaymentStatus) && CatalogRules.PaymentStatuses.Contains(PaymentStatus))
            query = query.Where(item => item.PaymentStatus == PaymentStatus);

        return query;
    }
}
