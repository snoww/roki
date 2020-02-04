using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Help.Common;
using Roki.Modules.Help.Services;

namespace Roki.Modules.Help
{
    public class Help : RokiTopLevelModule<HelpService>
    {
        private readonly CommandService _command;
        private readonly IServiceProvider _services;

        public Help(CommandService command, IServiceProvider services)
        {
            _command = command;
            _services = services;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Modules()
        {
            var embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("List of Modules")
                .WithDescription(string.Join("\n",
                    _command.Modules.GroupBy(module => module.GetTopLevelModule())
                        .Select(module => "• " + module.Key.Name)
                        .OrderBy(s => s)))
                .WithFooter("Use .commands <module> to see commands of that module");
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases, RokiOptions(typeof(CommandArgs))]
        public async Task Commands(string moduleName = null, params string[] args)
        {
            var opts = OptionsParser.ParseFrom(new CommandArgs(), args);

            moduleName = moduleName?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(moduleName))
                return;

            var commands = _command.Commands.Where(c =>
                    c.Module.GetTopLevelModule().Name.ToUpperInvariant().StartsWith(moduleName, StringComparison.InvariantCulture))
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

            var module = commands.GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.Ordinal))
                .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count())
                .ToList();

            if (!commands.Any())
            {
                if (opts.View != CommandArgs.ViewType.Hide)
                    await Context.Channel.SendErrorAsync("Module not found, use `.modules` to see the list of modules.").ConfigureAwait(false);
                else
                    await Context.Channel.SendErrorAsync("Module not found or you do not have permission to execute commands in that module.").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle($"{commands.First().Module.GetTopLevelModule().Name} Module Commands")
                .WithFooter($"Use {Roki.Properties.Prefix}h <command> to see the help for that command");

            foreach (var submodule in module)
            {
                var formatted = new StringBuilder();
                
                foreach (var command in submodule)
                {
                    var commandName = Roki.Properties.Prefix + command.Aliases.First();
                    var aliases = "[" + string.Join("/", command.Aliases.Skip(1)) + "]";
                    // command names should be less than 30 characters
                    var padding = Math.Abs(30 - commandName.Length);

                    if (opts.View == CommandArgs.ViewType.Cross)
                    {
                        formatted.AppendLine($"{(success.Contains(command) ? "✅" : "❌")}" +
                                             commandName + aliases.PadLeft(padding - 1, ' '));
                    }
                    else
                    {
                        formatted.AppendLine(commandName + aliases.PadLeft(padding, ' '));
                    }
                }
                
                embed.AddField(submodule.Key, $"```css\n{string.Join("\n", formatted)}\n```");
            }
            
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [Priority(0)]
        public async Task H([Leftover] string command = null)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                await H((CommandInfo) null).ConfigureAwait(false);
                return;
            }
            
            var cmd = _command.Commands.FirstOrDefault(x => x.Aliases.Any(name => name.ToLowerInvariant() == command));
            if (cmd != null)
            {
                await H(cmd).ConfigureAwait(false);
                return;
            }
            
            await Context.Channel.SendErrorAsync("Command not found.\nTry `.modules` to find the correct module, then `.commands <module>` to find the specific command.").ConfigureAwait(false);
        }
        
        private async Task H(CommandInfo command)
        {
            var channel = Context.Channel;

            if (command == null)
            {
                var helpEmbed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle("Roki Help")
                    .WithDescription(string.Format(@"Simple guide to find a command:
Use `{0}modules` command to see a list of all modules.
Then use `{0}commands <module>` to see a list of all the commands in that module (e.g. `{0}commands searches`).
After seeing the commands available in that module, you can use `{0}h <command>` to get help for a specific command (e.g. `{0}h weather`).", Roki.Properties.Prefix));
                
                if (channel is IDMChannel)
                {
                    await channel.EmbedAsync(helpEmbed).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        var dm = await ((IGuildUser) Context.User).GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        await dm.EmbedAsync(helpEmbed).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        await channel.EmbedAsync(helpEmbed).ConfigureAwait(false);
                    }
                }
            }

            await Service.SendCommandInfo(command, Context);
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