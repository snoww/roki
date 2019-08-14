using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extentions;
using Roki.Modules.Help.Common;

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
        public async Task Modules()
        {
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle("List of Modules")
                .WithDescription(string.Join("\n",
                        _command.Modules.GroupBy(module => module.GetTopLevelModule())
                            .Select(module => "• " + module.Key.Name)
                            .OrderBy(s => s)));
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        [RokiOptions(typeof(CommandOptions))]
        public async Task Commands(string module = null, params string[] args)
        {
            var (opts, _) = OptionParser.ParseFrom(new CommandOptions(), args);

            module = module?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(module))
                return;

            var cmds = _command.Commands.Where(cmd => cmd.Module.GetTopLevelModule().Name.ToUpperInvariant()
                    .StartsWith(module, StringComparison.InvariantCulture))
                    .OrderBy(commands => commands.Aliases[0])
                    .Distinct(new CommandTextEqualityComparer());

            var succuss = new HashSet<CommandInfo>();
            if(opts.View != CommandOptions.ViewType.All){
                succuss = new HashSet<CommandInfo>((await Task.WhenAll(cmds.Select(async x =>
                {
                    var pre = await x.CheckPreconditionsAsync(Context, _service).ConfigureAwait(false);
                    return (Command: x, Succuss: pre.IsSuccess);
                })).ConfigureAwait(false))
                    .Where(x => x.Succuss)
                    .Select(x => x.Command));

                if (opts.View == CommandOptions.ViewType.Hide)
                {
                    cmds = cmds.Where((x => succuss.Contains(x)));
                }
            }

            var cmdsWithGroup = cmds
                .GroupBy(cmd => cmd.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
                .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());

            if (!cmds.Any())
            {
                if (opts.View != CommandOptions.ViewType.Hide)
                {
                    var errEmbed = new EmbedBuilder().WithErrorColor()
                        .WithDescription("Module not found");
                    await ctx.Channel.EmbedAsync(errEmbed).ConfigureAwait(false);
                }
                else
                {
                    var errEmbed = new EmbedBuilder().WithErrorColor()
                        .WithDescription("Module not found or can't execute");
                    await ctx.Channel.EmbedAsync(errEmbed).ConfigureAwait(false);
                }
                return;
            }

            var i = 0;
            var groups = cmdsWithGroup.GroupBy(x => i++ / 48).ToArray();
            var embed = new EmbedBuilder().WithOkColor();
            foreach (var group in groups)
            {
                var last = group.Count();
                for (i = 0; i < last; i++)
                {
                    var transformed = group.ElementAt(i).Select(x =>
                    {
                        if (opts.View == CommandOptions.ViewType.Cross)
                        {
                            return $"{(succuss.Contains(x) ? "✅" : "❌")}{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                        }

                        return $"{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                    });

                    if (i == last - 1 && (i + 1) % 2 != 0)
                    {
                        var grp = 0;
                        var count = transformed.Count();
                        transformed = transformed
                            .GroupBy(x => grp++ % count / 2)
                            .Select(x =>
                            {
                                if (x.Count() == 1)
                                    return $"{x.First()}";
                                return String.Concat(x);
                            });
                    }
                    embed.AddField(group.ElementAt(i).Key, "```css\n" + string.Join("\n", transformed) + "\n```", true);
                }
            }

            embed.WithFooter(String.Format("Type `{0}h CommandName` to see the help for that specified command. e.g. `{0}h {0}8ball`", Prefix));
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
        {
            public bool Equals(CommandInfo x, CommandInfo y) => x.Aliases[0] == y.Aliases[0];

            public int GetHashCode(CommandInfo obj) => obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);

        }

    }
}