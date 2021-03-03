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
    [Route("manage")]
    public class GuildController : Controller
    {
        private readonly RokiService _rokiService;
        private readonly DiscordService _discordService;

        public GuildController(RokiService rokiService, DiscordService discordService)
        {
            _rokiService = rokiService;
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
            Guild guild = await _rokiService.GetRokiGuild(guildId);
            if (guild == null)
            {
                return View("Manage");
            }

            List<ChannelSummary> channels = await _rokiService.GetGuildChannels(guildId);

            return View("Settings", new GuildChannelModel{Section = "_CoreSettings", Guild = guild, Channels = channels});
        }

        [Route("{guildId}/modules")]
        public async Task<IActionResult> ModuleSettings(ulong guildId)
        {
            Guild guild = await _rokiService.GetRokiGuild(guildId);
            if (guild == null)
            {
                return View("Manage");
            }

            List<ChannelSummary> channels = await _rokiService.GetGuildChannels(guildId);

            return View("Settings", new GuildChannelModel{Section = "_ModuleSettings", Guild = guild, Channels = channels});
        }
        
        [Route("{guildId}/currency")]
        public async Task<IActionResult> CurrencySettings(ulong guildId)
        {
            Guild guild = await _rokiService.GetRokiGuild(guildId);
            if (guild == null)
            {
                return View("Manage");
            }

            List<ChannelSummary> channels = await _rokiService.GetGuildChannels(guildId);

            return View("Settings", new GuildChannelModel{Section = "_CurrencySettings", Guild = guild, Channels = channels});
        }
        
        [Route("{guildId}/xp")]
        public async Task<IActionResult> XpSettings(ulong guildId)
        {
            Guild guild = await _rokiService.GetRokiGuild(guildId);
            if (guild == null)
            {
                return View("Manage");
            }

            List<ChannelSummary> channels = await _rokiService.GetGuildChannels(guildId);

            return View("Settings", new GuildChannelModel{Section = "_XpSettings", Guild = guild, Channels = channels});
        }
        
        [Route("{guildId}/{channelId}")]
        public async Task<IActionResult> ChannelSettings(ulong guildId, ulong channelId)
        {
            Guild guild = await _rokiService.GetRokiGuild(guildId);
            if (guild == null)
            {
                return View("Manage");
            }
            
            Channel channel = await _rokiService.GetGuildChannel(guildId, channelId);
            if (channel == null)
            {
                // todo show 404
            }
            
            List<ChannelSummary> channels = await _rokiService.GetGuildChannels(guildId);

            return View("Settings", new GuildChannelModel{Section = "_ChannelSettings", ChannelId = channelId, Guild = guild, Channels = channels, Channel = channel});
        }
    }
}