using Microsoft.AspNetCore.Mvc;
using WebShop.Infrastructure;

namespace WebShop.Pages.Shared.Components.HtmlBlock;

public class HtmlBlockViewComponent(HtmlBlockService blocks) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string key)
    {
        var html = await blocks.GetHtmlAsync(key);
        if (string.IsNullOrWhiteSpace(html))
            return Content(string.Empty);

        return View("Default", html);
    }
}
