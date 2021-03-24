using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roki.Web.Models;
using Roki.Web.Services;

namespace Roki.Web.Controllers
{
    [Authorize]
    [Route("manage")]
    public class GuildController : Controller
    {
        private readonly RokiContext _context;
        private readonly DiscordService _discordService;

        public GuildController(RokiContext context, DiscordService discordService)
        {
            _context = context;
            _discordService = discordService;
        }

        [Route("")]
        public async Task<IActionResult> Manage()
        {
            string accessToken = await HttpContext.GetTokenAsync("Discord", "access_token");
            List<DiscordGuild> response = await _discordService.GetOwnerGuilds(accessToken);
            return View(response);
        }
        
        [Route("{guildId}")]
        public async Task<IActionResult> GuildSettings(ulong guildId)
        {
            GuildConfig config = await _context.GuildConfigs.AsNoTracking().Include(x => x.Guild).SingleOrDefaultAsync(x => x.GuildId == guildId);
            if (config == null)
            {
                return Redirect("/manage");
            }

            return View("GuildSettings", config);
        }

        [Route("{guildId}/channels")]
        public async Task<IActionResult> ChannelSettings(ulong guildId)
        {
            ulong userId = ulong.Parse(User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"));
            Guild guild = await _context.Guilds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guildId && (x.OwnerId == userId || x.Moderators.Contains((long) userId)));
            if (guild == null)
            {
                return Redirect("/manage");
            }

            List<Channel> channels = await _context.Channels.AsNoTracking().Where(x => x.GuildId == guildId).OrderBy(x => x.Name).ToListAsync();
            
            if (channels.Count == 0)
            {
                return Redirect($"/manage/{guildId}");
            }
            return View("ChannelSettings", new ChannelViewModel{Guild = guild, Channels = channels});
        }
    }
}