using Microsoft.AspNetCore.Mvc;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shared.Components.ShopHeader;

public class ShopHeaderViewComponent(ShopStore store) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() => View(await store.GetAsync());
}
