using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shop;

public class CartModel(CartService cart) : PageModel
{
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];

    public decimal Total => Lines.Sum(line => line.UnitPrice * line.Quantity);

    public void OnGet() => Lines = cart.Get();

    public IActionResult OnPostUpdate(int productId, int quantity)
    {
        cart.SetQuantity(productId, quantity);
        return RedirectToPage();
    }

    public IActionResult OnPostRemove(int productId)
    {
        cart.Remove(productId);
        return RedirectToPage();
    }
}
