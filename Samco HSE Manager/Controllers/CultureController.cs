using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Samco_HSE_Manager.Controllers
{
    [Route("Culture/[action]")]
    public class CultureController : Controller
    {
        public IActionResult SetCulture(string culture, string redirectUri)
        {
            if (!string.IsNullOrEmpty(culture))
            {
                var requestCulture = new RequestCulture(culture, culture);
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(requestCulture));
            }

            return LocalRedirect(redirectUri);
        }
    }
}
