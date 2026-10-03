using Microsoft.AspNetCore.Mvc;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shared.Components.ShopFooter;

public class ShopFooterViewComponent(ShopStore store) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() => View(await store.GetAsync());
}
