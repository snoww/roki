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
            await _service.RetrieveMissingMessages(ctx, afterMessage).ConfigureAwait(false);
        }
    }
}