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
        public async Task Fill(ulong afterMessage = 649060390386401300)
        {
            var count = await _service.FillMissingMessagesAsync(afterMessage).ConfigureAwait(false);
            var str = "";
            foreach (var (channel, num) in count)
            {
                str += $"{channel}: `{num}` messages added\n";
            }
            await ctx.Channel.SendMessageAsync(str).ConfigureAwait(false);
        }
    }
}