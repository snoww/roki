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
    [Route("manage")]
    public class SettingsController : ControllerBase
    {
        private readonly RokiContext _context;

        public SettingsController(RokiContext context)
        {
            _context = context;
        }

        [HttpPost("{guildId}/settings")]
        public async Task<IActionResult> UpdateCoreSettings(ulong guildId, [FromBody] GuildConfigUpdate model)
        {
            if (model == null)
            {
                return BadRequest(new {error = "Not all fields are filled"});
            }

            GuildConfig config = await _context.GuildConfigs.SingleOrDefaultAsync(x => x.GuildId == guildId);
            Dictionary<string, string> errors = ValidationService.ValidateGuildConfig(model, config);
            if (errors.Count > 0)
            {
                return BadRequest(errors);
            }

            await _context.SaveChangesAsync();
            
            return Ok();
        }
    }
}