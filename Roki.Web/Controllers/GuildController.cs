using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Roki.Web.Models;
using Roki.Web.Services;

namespace Roki.Web.Controllers
{
    [Authorize]
    public class GuildController : Controller
    {
        private readonly RokiService _rokiService;
        private readonly DiscordService _discordService;

        public GuildController(RokiService rokiService, DiscordService discordService)
        {
            _rokiService = rokiService;
            _discordService = discordService;
        }

        [Route("manage")]
        public async Task<IActionResult> Manage()
        {
            string accessToken = await HttpContext.GetTokenAsync("Discord", "access_token");
            List<DiscordGuild> response = await _discordService.GetOwnerGuilds(accessToken);
            return View(response);
        }
        
        [Route("manage/{guildId}")]
        public async Task<IActionResult> Manage(ulong guildId)
        {
            // if (!(User?.Identity?.IsAuthenticated ?? false))
            // {
            //     return Redirect("/login?redirect");
            // }
            Guild guild = await _rokiService.GetRokiGuild(guildId);
            if (guild == null)
            {
                return View();
            }

            return View("Guild", guild);
        }
    }
}