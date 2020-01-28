using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Help.Common;
using Roki.Modules.Help.Services;
using Roki.Services;

namespace Roki.Modules.Help
{
    public class Help : RokiTopLevelModule<HelpService>
    {
        private readonly CommandService _command;
        private readonly IRokiConfig _config;
        private readonly IServiceProvider _services;

        public Help(IRokiConfig config, CommandService command, IServiceProvider services)
        {
            _config = config;
            _command = command;
            _services = services;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Modules()
        {
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle("List of Modules")
                .WithDescription(string.Join("\n",
                    _command.Modules.GroupBy(module => module.GetTopLevelModule())
                        .Select(module => "• " + module.Key.Name)
                        .OrderBy(s => s)))
                .WithFooter("Use .commands <module> to see commands of that module");
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases, RokiOptions(typeof(CommandOptions))]
        public async Task Commands(string module = null, params string[] args)
        {
            var (opts, _) = OptionsParser.ParseFrom(new CommandOptions(), args);

            module = module?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(module))
                return;

            var cmds = _command.Commands.Where(cmd => cmd.Module.GetTopLevelModule().Name.ToUpperInvariant()
                    .StartsWith(module, StringComparison.InvariantCulture))
                .OrderBy(commands => commands.Aliases[0])
                .Distinct(new CommandTextEqualityComparer());

            var success = new HashSet<CommandInfo>();
            if (opts.View != CommandOptions.ViewType.All)
            {
                success = new HashSet<CommandInfo>((await Task.WhenAll(cmds.Select(async x =>
                    {
                        var pre = await x.CheckPreconditionsAsync(Context, _services).ConfigureAwait(false);
                        return (Command: x, Success: pre.IsSuccess);
                    })).ConfigureAwait(false))
                    .Where(x => x.Success)
                    .Select(x => x.Command));

                if (opts.View == CommandOptions.ViewType.Hide) cmds = cmds.Where(x => success.Contains(x));
            }

            var cmdsWithGroup = cmds.GroupBy(cmd => cmd.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
                .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());

            if (!cmds.Any())
            {
                if (opts.View != CommandOptions.ViewType.Hide)
                    await ctx.Channel.SendErrorAsync("Module not found, use `.modules` to see the list of modules.").ConfigureAwait(false);
                else
                    await ctx.Channel.SendErrorAsync("Module not found or can't execute");
                return;
            }

            var i = 0;
            var groups = cmdsWithGroup.GroupBy(x => i++ / 48).ToArray();
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle($"{cmds.First().Module.GetTopLevelModule().Name} Module Commands")
                .WithFooter($"Use {Roki.Properties.Prefix}h <command> to see the help for that command");

            foreach (var group in groups)
            {
                var last = group.Count();
                for (i = 0; i < last; i++)
                {
                    var transformed = group.ElementAt(i).Select(x => opts.View == CommandOptions.ViewType.Cross 
                        ? $"{(success.Contains(x) ? "✅" : "❌")}{Roki.Properties.Prefix + x.Aliases.First(),-18} {"[" + string.Join("/", x.Aliases.Skip(1)) + "]",10}\n" 
                        : $"{Roki.Properties.Prefix + x.Aliases.First(),-18} {"[" + string.Join("/", x.Aliases.Skip(1)) + "]",10}");

                    embed.AddField(group.ElementAt(i).Key, "```css\n" + string.Join("\n", transformed) + "\n```");
                }
            }
            
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [Priority(0)]
        public async Task H([Leftover] string fail)
        {
            var prefixless = _command.Commands.FirstOrDefault(x => x.Aliases.Any(cmdName => cmdName.ToLowerInvariant() == fail));
            if (prefixless != null)
            {
                await H(prefixless).ConfigureAwait(false);
                return;
            }
            
            await ctx.Channel.SendErrorAsync("Command not found.\nTry `.modules` to find the correct module, then `.commands <module>` to find the specific command.").ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [Priority(1)]
        public async Task H([Leftover] CommandInfo cmd = null)
        {
            var channel = ctx.Channel;

            if (cmd == null)
            {
                var ch = channel is ITextChannel
                    ? await ((IGuildUser) ctx.User).GetOrCreateDMChannelAsync().ConfigureAwait(false)
                    : channel;

                await ch.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("Roki Help")
                    // TODO add this in botconfig
                    .WithDescription(string.Format(@"
You can use `{0}modules` command to see a list of all modules.
You can use `{0}commands <module>` to see a list of all the commands in that module (e.g. `{0}commands Utility`).
You can use `{0}h <command>` to get help for a specific command (e.g. `{0}h listquotes`).
", Roki.Properties.Prefix))).ConfigureAwait(false);
            }

            var embed = _service.GetCommandHelp(cmd, ctx.Guild);
            await channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
        {
            public bool Equals(CommandInfo x, CommandInfo y)
            {
                return x.Aliases[0] == y.Aliases[0];
            }

            public int GetHashCode(CommandInfo obj)
            {
                return obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);
            }
        }
    }
}