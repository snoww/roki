using System;
using System.Diagnostics;
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
        [OwnerOnly]
        public async Task Fill(ulong afterMessage = 657601009455071232)
        {
            var sw = Stopwatch.StartNew();
            await _service.FillMissingMessagesAsync(afterMessage).ConfigureAwait(false);
            sw.Stop();
            await ctx.Channel.SendMessageAsync($"Finished adding missing messages, took {Format.Code(sw.Elapsed.ToReadableString())}");
        }
    }
}