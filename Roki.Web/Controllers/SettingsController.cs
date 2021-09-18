using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roki.Web.Models;
using Roki.Web.Services;

namespace Roki.Web.Controllers
{
    [ApiController]
    [Authorize]
    [Route("manage/{guildId}")]
    public class SettingsController : ControllerBase
    {
        private readonly RokiContext _context;

        public SettingsController(RokiContext context)
        {
            _context = context;
        }

        [HttpPost("settings")]
        public async Task<IActionResult> UpdateCoreSettings(ulong guildId, [FromBody] GuildConfigUpdate model)
        {
            if (model == null)
            {
                return BadRequest(new {error = "Not all fields are filled"});
            }

            GuildConfig config = await _context.GuildConfigs.SingleOrDefaultAsync(x => x.GuildId == guildId);
            if (config == null)
            {
                return NotFound();
            }
            Dictionary<string, string> errors = ValidationService.ValidateGuildConfig(model, config);
            if (errors.Count > 0)
            {
                return BadRequest(errors);
            }

            await _context.SaveChangesAsync();
            
            return Ok();
        }

        [HttpGet("channels/{channelId}")]
        public async Task<IActionResult> GetChannelSettings(ulong channelId)
        {
            ChannelConfig channelConfig = await _context.ChannelConfigs.AsNoTracking().Include(x => x.Channel).SingleOrDefaultAsync(x => x.ChannelId == channelId);
            if (channelConfig == null)
            {
                return NotFound();
            }

            return Ok(new {channelConfig.Logging, channelConfig.CurrencyGen, channelConfig.XpGain, channelConfig.Channel.DeletedDate});
        }
        
        [HttpPost("channels/{channelId}")]
        public async Task<IActionResult> UpdateChannelSettings(ulong channelId, [FromBody] ChannelConfigUpdate model)
        {
            ChannelConfig config = await _context.ChannelConfigs.SingleOrDefaultAsync(x => x.ChannelId == channelId);
            config.Logging = model.Logging == "on";
            config.CurrencyGen = model.CurrencyGen == "on";
            config.XpGain = model.XpGain == "on";

            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}