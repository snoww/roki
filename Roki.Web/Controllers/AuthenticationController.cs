using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Roki.Web.Controllers
{
    [Authorize]
    public class AuthenticationController : Controller
    {
        [AllowAnonymous]
        [Route("login")]
        public IActionResult Login(string redirect)
        {
            if (string.IsNullOrWhiteSpace(redirect))
            {
                redirect = "/";
            }
            return Challenge(new AuthenticationProperties { RedirectUri = redirect }, "Discord");
        }

        [Route("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return Redirect("/");
        }
    }
}