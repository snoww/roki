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

        [RokiCommand, Description, Usage, Aliases, RokiOptions(typeof(CommandArgs))]
        public async Task Commands(string module = null, params string[] args)
        {
            var opts = OptionsParser.ParseFrom(new CommandArgs(), args);

            module = module?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(module))
                return;

            var commands = _command.Commands.Where(c =>
                    c.Module.GetTopLevelModule().Name.ToUpperInvariant().StartsWith(module, StringComparison.InvariantCulture))
                .OrderBy(c => c.Aliases[0])
                .Distinct(new CommandTextEqualityComparer())
                .ToList();

            var success = new HashSet<CommandInfo>();
            if (opts.View != CommandArgs.ViewType.All)
            {
                success = new HashSet<CommandInfo>((await Task.WhenAll(commands.Select(async x =>
                    {
                        var precondition = await x.CheckPreconditionsAsync(Context, _services).ConfigureAwait(false);
                        return (Command: x, Success: precondition.IsSuccess);
                    })).ConfigureAwait(false))
                    .Where(x => x.Success)
                    .Select(x => x.Command));

                if (opts.View == CommandArgs.ViewType.Hide) 
                    commands = commands.Where(x => success.Contains(x)).ToList();
            }

            var commandsGroup = commands.GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.Ordinal))
                .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count())
                .ToList();

            if (!commands.Any())
            {
                if (opts.View != CommandArgs.ViewType.Hide)
                    await ctx.Channel.SendErrorAsync("Module not found, use `.modules` to see the list of modules.").ConfigureAwait(false);
                else
                    await ctx.Channel.SendErrorAsync("Module not found or you do not have permission to execute commands in that module.");
                return;
            }

            var i = 0;
            var groups = commandsGroup.GroupBy(x => i++ / 48).ToArray();
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle($"{commands.First().Module.GetTopLevelModule().Name} Module Commands")
                .WithFooter($"Use {Roki.Properties.Prefix}h <command> to see the help for that command");

            foreach (var group in groups)
            {
                var last = group.Count();
                for (i = 0; i < last; i++)
                {
                    var transformed = group.ElementAt(i).Select(x => opts.View == CommandArgs.ViewType.Cross 
                        ? $"{(success.Contains(x) ? "✅" : "❌")}{Roki.Properties.Prefix + x.Aliases.First(),-18} {"[" + string.Join("/", x.Aliases.Skip(1)) + "]",10}\n" 
                        : $"{Roki.Properties.Prefix + x.Aliases.First(),-18} {"[" + string.Join("/", x.Aliases.Skip(1)) + "]",10}");

                    embed.AddField(group.ElementAt(i).Key, $"```css\n{string.Join("\n", transformed)}\n```");
                }
            }
            
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [Priority(0)]
        public async Task H([Leftover] string command)
        {
            var cmd = _command.Commands.FirstOrDefault(x => x.Aliases.Any(name => name.ToLowerInvariant() == command));
            if (cmd != null)
            {
                await H(cmd).ConfigureAwait(false);
                return;
            }
            
            await ctx.Channel.SendErrorAsync("Command not found.\nTry `.modules` to find the correct module, then `.commands <module>` to find the specific command.").ConfigureAwait(false);
        }

        private async Task H([Leftover] CommandInfo cmd = null)
        {
            var channel = ctx.Channel;

            if (cmd == null)
            {
                var helpEmbed = new EmbedBuilder().WithOkColor()
                    .WithTitle("Roki Help")
                    .WithDescription(string.Format(@"
You can use `{0}modules` command to see a list of all modules.
You can use `{0}commands <module>` to see a list of all the commands in that module (e.g. `{0}commands Utility`).
You can use `{0}h <command>` to get help for a specific command (e.g. `{0}h listquotes`).
", Roki.Properties.Prefix));
                
                if (channel is IDMChannel)
                {
                    await channel.EmbedAsync(helpEmbed).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        var dm = await ((IGuildUser) ctx.User).GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        await dm.EmbedAsync(helpEmbed).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        await channel.EmbedAsync(helpEmbed).ConfigureAwait(false);
                    }
                }
            }

            var embed = _service.GetCommandHelp(cmd, ctx.Guild);
            await channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
        {
            public bool Equals(CommandInfo x, CommandInfo y)
            {
                return x?.Aliases[0] == y?.Aliases[0];
            }

            public int GetHashCode(CommandInfo obj)
            {
                return obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);
            }
        }
    }
}