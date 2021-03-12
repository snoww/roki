using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Help.Services;
using Roki.Services;

namespace Roki.Modules.Help
{
    public class Help : RokiTopLevelModule<HelpService>
    {
        private readonly CommandService _command;
        private readonly IServiceProvider _services;
        private readonly IConfigurationService _config;

        public Help(CommandService command, IServiceProvider services, IConfigurationService config)
        {
            _command = command;
            _services = services;
            _config = config;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Modules()
        {
            string prefix = Context.Channel is IDMChannel ? Roki.Properties.Prefix : (await _config.GetGuildConfigAsync(Context.Guild.Id)).Prefix;
            
            EmbedBuilder embed = new EmbedBuilder().WithOkColor()
                .WithTitle("List of Modules")
                .WithDescription(string.Join("\n",
                    _command.Modules.GroupBy(module => module.GetTopLevelModule())
                        .Select(module => "• " + module.Key.Name)
                        .OrderBy(s => s)))
                .WithFooter($"Use {prefix}commands <module> to see commands of that module");
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Commands(string moduleName = null)
        {
            moduleName = moduleName?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return;
            }
            
            string prefix = Context.Channel is IDMChannel ? Roki.Properties.Prefix : (await _config.GetGuildConfigAsync(Context.Guild.Id)).Prefix;

            List<CommandInfo> commands = _command.Commands.Where(c =>
                    c.Module.GetTopLevelModule().Name.ToUpperInvariant().StartsWith(moduleName, StringComparison.InvariantCulture))
                .OrderBy(c => c.Aliases[0])
                .Distinct(new CommandTextEqualityComparer())
                .ToList();

            var success = new HashSet<CommandInfo>((await Task.WhenAll(commands.Select(async x =>
                {
                    PreconditionResult precondition = await x.CheckPreconditionsAsync(Context, _services).ConfigureAwait(false);
                    return (Command: x, Success: precondition.IsSuccess);
                })).ConfigureAwait(false))
                .Where(x => x.Success)
                .Select(x => x.Command));
            
            commands = commands.Where(x => success.Contains(x)).ToList();

            List<IGrouping<string, CommandInfo>> module = commands.GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.Ordinal))
                .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count())
                .ToList();

            if (!commands.Any())
            {
                await Context.Channel.SendErrorAsync($"Module not found, use `{prefix}modules` to see the list of modules.").ConfigureAwait(false);
                return;
            }

            EmbedBuilder embed = new EmbedBuilder().WithOkColor()
                .WithTitle($"{commands.First().Module.GetTopLevelModule().Name} Module Commands")
                .WithFooter($"Use {prefix}h <command> to see the help for that command");

            foreach (IGrouping<string, CommandInfo> submodule in module)
            {
                var formatted = new StringBuilder();

                foreach (CommandInfo command in submodule)
                {
                    string commandName = prefix + command.Aliases[0];
                    string aliases = "[" + string.Join("/", command.Aliases.Skip(1)) + "]";
                    // command names should be less than 30 characters
                    int padding = Math.Abs(30 - commandName.Length);
                    formatted.AppendLine(commandName + aliases.PadLeft(padding, ' '));
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

            CommandInfo cmd = _command.Commands.FirstOrDefault(x => x.Aliases.Any(name => name.ToLowerInvariant() == command));
            if (cmd != null)
            {
                await H(cmd).ConfigureAwait(false);
                return;
            }
            
            string prefix = Context.Guild != null ? (await _config.GetGuildConfigAsync(Context.Guild.Id)).Prefix : Roki.Properties.Prefix;

            await Context.Channel.SendErrorAsync($"Command not found.\nTry `{prefix}modules` to find the correct module, then `{prefix}commands <module>` to find the specific command.").ConfigureAwait(false);
        }

        private async Task H(CommandInfo command)
        {
            string prefix = Context.Channel is IDMChannel ? Roki.Properties.Prefix : (await _config.GetGuildConfigAsync(Context.Guild.Id)).Prefix;

            if (command == null)
            {
                EmbedBuilder helpEmbed = new EmbedBuilder().WithOkColor()
                    .WithTitle("Roki Help")
                    .WithDescription(string.Format(@"Simple guide to find a command:
Use `{0}modules` command to see a list of all modules.
Then use `{0}commands <module>` to see a list of all the commands in that module (e.g. `{0}commands searches`).
After seeing the commands available in that module, you can use `{0}h <command>` to get help for a specific command (e.g. `{0}h weather`).", prefix));

                if (Context.Channel is IDMChannel)
                {
                    await Context.Channel.EmbedAsync(helpEmbed).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        IDMChannel dm = await ((IGuildUser) Context.User).GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        await dm.EmbedAsync(helpEmbed).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        await Context.Channel.EmbedAsync(helpEmbed).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                await HelpService.SendCommandInfo(command, Context, prefix);
            }
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