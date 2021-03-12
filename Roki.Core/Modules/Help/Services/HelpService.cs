using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Help.Services
{
    public class HelpService : IRokiService
    {
        private readonly IConfigurationService _config;

        public HelpService(CommandHandler command, IConfigurationService config)
        {
            _config = config;
            command.CommandOnError += CommandOnError;
        }

        private async Task CommandOnError(ExecuteCommandResult result)
        {
            if (result.Result.Error == CommandError.BadArgCount || result.Result.Error == CommandError.ParseFailed)
            {
                if (result.Context.Channel is IDMChannel)
                {
                    await SendCommandInfo(result.CommandInfo, result.Context, Roki.Properties.Prefix);
                }
                else
                {
                    await SendCommandInfo(result.CommandInfo, result.Context, await _config.GetGuildPrefix(result.Context.Guild.Id));
                }
            }
        }

        public static async Task SendCommandInfo(CommandInfo command, ICommandContext context, string prefix)
        {
            var str = $"**`{prefix + command.Aliases[0]}`**";
            string aliases = string.Join("/", command.Aliases.Skip(1).Select(a => $"`{a}`"));
            if (!string.IsNullOrWhiteSpace(aliases))
            {
                str += $"/{aliases}";
            }

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(context).AddField(str, command.FormatSummary(prefix), true)
                .AddField("Examples", command.FormatRemarks(prefix))
                .WithFooter($"Module: {command.Module.GetTopLevelModule().Name}");

            string requirements = GetCommandRequirements(command);
            if (!string.IsNullOrWhiteSpace(requirements))
            {
                embed.AddField("Requires", requirements);
            }

            Type options = ((RokiOptions) command.Attributes.FirstOrDefault(x => x is RokiOptions))?.OptionType;
            if (options != null)
            {
                string optionsHelp = GetCommandOptions(options);
                if (!string.IsNullOrWhiteSpace(optionsHelp))
                {
                    embed.AddField("Options", optionsHelp);
                }
            }

            await context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private static string GetCommandOptions(Type option)
        {
            IEnumerable<string> options = option.GetProperties()
                .Select(x => x.GetCustomAttributes(true).FirstOrDefault(a => a is OptionAttribute))
                .Where(x => x != null)
                .Cast<OptionAttribute>()
                .Select(x =>
                {
                    string optionString = !string.IsNullOrWhiteSpace(x.ShortName) 
                        ? $"`-{x.ShortName}`, `--{x.LongName}`" 
                        : $"`--{x.LongName}`";

                    optionString += $" {x.HelpText}";
                    return optionString;
                });
            return string.Join("\n", options);
        }

        private static string GetCommandRequirements(CommandInfo command)
        {
            IEnumerable<string> requirements = command.Preconditions
                .Where(a => a is RequireUserPermissionAttribute || a is OwnerOnly)
                .Select(a =>
                {
                    if (a is OwnerOnly)
                    {
                        return "Bot Owner";
                    }

                    var perm = (RequireUserPermissionAttribute) a;
                    return perm.GuildPermission != null
                        ? $"{perm.GuildPermission} Server Permission".Replace("Guild", "Server", StringComparison.Ordinal)
                        : $"{perm.ChannelPermission} Channel Permission";
                });

            return string.Join("\n", requirements);
        }
    }
}