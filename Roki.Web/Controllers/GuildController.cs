using System;
using System.Collections.Generic;
using System.Linq;
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
            GuildConfig config = await _context.GuildConfigs.AsNoTracking().Include(x => x.Guild).Where(x => x.GuildId == guildId).SingleOrDefaultAsync();
            if (config == null)
            {
                return View("Manage");
            }

            List<Channel> channels = await _context.Channels.AsNoTracking().Where(x => x.GuildId == guildId).ToListAsync();

            return View("Settings", new GuildChannelModel{GuildConfig = config, Channels = channels});
        }

        [HttpPost("{guildId}/settings")]
        public async Task<IActionResult> UpdateCoreSettings(ulong guildId, [FromBody] GuildConfigUpdate model)
        {
            if (model.Prefix.Length > 2)
            {
                return BadRequest(new {error = "Prefix must be less than 5 characters long."});
            }
            
            if (model.CurrencyName.Length > 50 || model.CurrencyNamePlural.Length > 51)
            {
                return BadRequest(new {error = "Currency Name must be less than 50 characters long."});
            }

            if (model.CurrencyGenerationChance < 0 || model.CurrencyGenerationChance > 1)
            {
                return BadRequest(new {error = "Currency Generation Chance must be between 0 and 1 (inclusive)."});
            }
            // GuildConfig guildConfig = await _context.GuildConfigs.SingleOrDefaultAsync(x => x.GuildId == guildId);
            // guildConfig.Prefix = model.Prefix;
            // guildConfig.Logging = model.Logging == "on";
            // guildConfig.XpGain = model.XpGain == "on";
            // guildConfig.CurrencyGen = model.CurrencyGen == "on";
            // guildConfig.ShowHelpOnError = model.ShowHelpOnError == "on";
            // await _context.SaveChangesAsync();
            return Ok(new {status = "ok"});
        }

        // [Route("{guildId}/{channelId}")]
        // public async Task<IActionResult> ChannelSettings(ulong guildId, ulong channelId)
        // {
        //     Guild guild = await _context.GetRokiGuild(guildId);
        //     if (guild == null)
        //     {
        //         return View("Manage");
        //     }
        //     
        //     Channel channel = await _context.GetGuildChannel(guildId, channelId);
        //     if (channel == null)
        //     {
        //         // todo show 404
        //     }
        //     
        //     List<ChannelSummary> channels = await _context.GetGuildChannels(guildId);
        //
        //     return View("Settings", new GuildChannelModel{Section = "_ChannelSettings", ChannelId = channelId, Guild = guild, Channels = channels, Channel = channel});
        // }
    }
}