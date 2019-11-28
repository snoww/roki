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
        public async Task Fill(ulong afterMessage = 646848809707503666)
        {
            var start = DateTime.UtcNow;
            var count = await _service.FillMissingMessagesAsync(afterMessage).ConfigureAwait(false);
            var end = DateTime.UtcNow - start;
            await ctx.Channel.SendMessageAsync($"Took {Format.Code(end.ToReadableString())}");
            var str = "";
            foreach (var (channel, num) in count)
            {
                str += $"{channel}: `{num}` messages added\n";
            }
            await ctx.Channel.SendMessageAsync(str).ConfigureAwait(false);
        }
    }
}