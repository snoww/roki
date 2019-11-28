using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Administration.Services;

namespace Roki.Modules.Administration
{
    public class Administration : RokiTopLevelModule<AdministrationService>
    {
        [RokiCommand, Description, Usage, Aliases]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Fill(ulong afterMessage = 649101719569039362)
        {
            var start = DateTime.UtcNow;
            await _service.FillMissingMessagesAsync(afterMessage).ConfigureAwait(false);
            var end = DateTime.UtcNow - start;
            await ctx.Channel.SendMessageAsync($"Took {Format.Code(end.ToReadableString())}");
        }
    }
}