using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extentions;

namespace Roki.Modules.Help
{
    public class Help : RokiTopLevelModule
    {
        private readonly IConfiguration _config;
        private readonly CommandService _command;
        private readonly IServiceProvider _service;

        public Help(IConfiguration config, CommandService command, IServiceProvider service)
        {
            _config = config;
            _command = command;
            _service = service;
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Modlues()
        {
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle("List of Modules")
                .WithDescription(string.Join("\n",
                        _command.Modules.GroupBy(module => module.GetTopLevelModule())
                            .Select(module => "• " + module.Key.Name)
                            .OrderBy(s => s)));
            await ctx.Channel.EmbedAsync(embed);
        }
        
    }
}