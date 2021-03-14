using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            if (result.Context.Channel is not IDMChannel && !(await _config.GetGuildConfigAsync(result.Context.Guild.Id)).ShowHelpOnError)
            {
                return;
            }
            
            string errorMessage;
            var showUsage = true;
            var showOptions = true;
            switch (result.Result.Error)
            {
                case CommandError.UnmetPrecondition:
                    showUsage = false;
                    showOptions = false;
                    if (result.Result.ErrorReason.Contains("context", StringComparison.Ordinal))
                    {
                        errorMessage = "This command can only be run in Servers";
                    }
                    else if (result.Result.ErrorReason.Contains("permissions"))
                    {
                        errorMessage = result.Result.ErrorReason.Replace("guild", "server", StringComparison.Ordinal);
                    }
                    else
                    {
                        errorMessage = result.Result.ErrorReason;
                    }
                    break;
                case CommandError.BadArgCount:
                    errorMessage = result.Result.ErrorReason;
                    break;
                case CommandError.ObjectNotFound:
                    showUsage = false;
                    showOptions = false;
                    errorMessage = result.Result.ErrorReason;
                    break;
                case CommandError.ParseFailed:
                    errorMessage = "Invalid arguments provided.";
                    break;
                default:
                    return;
            }
            
            if (result.Context.Channel is IDMChannel)
            {
                await SendErrorInfo(result, Roki.Properties.Prefix, errorMessage, showUsage, showOptions);
            }
            else
            {
                await SendErrorInfo(result, await _config.GetGuildPrefix(result.Context.Guild.Id), errorMessage, showUsage, showOptions);
            }
        }
        
        private static async Task SendErrorInfo(ExecuteCommandResult result, string prefix, string customError, bool showUsage, bool showOptions)
        {
            var str = $"**`{prefix + result.CommandInfo.Aliases[0]}`**";
            string aliases = string.Join("/", result.CommandInfo.Aliases.Skip(1).Select(a => $"`{a}`"));
            if (!string.IsNullOrWhiteSpace(aliases))
            {
                str += $"/{aliases}";
            }
            
            EmbedBuilder embed = new EmbedBuilder().WithTitle(str)
                .WithErrorColor()
                .WithDescription($"```Error: {customError}```")
                .WithFooter($"Module: {result.CommandInfo.Module.GetTopLevelModule().Name}");

            if (showUsage)
            {
                string[] remarks = result.CommandInfo.Remarks.Deserialize<string[]>().Select(x => string.Format(x, prefix)).ToArray();
                embed.AddField("Correct Usage", $"```{remarks[0]}```");
                if (remarks.Length > 1)
                {
                    embed.AddField("Examples", $"```{string.Join('\n', remarks[1..])}```");
                }
            }

            if (showOptions)
            {
                Type options = ((RokiOptions) result.CommandInfo.Attributes.FirstOrDefault(x => x is RokiOptions))?.OptionType;
                if (options != null)
                {
                    string optionsHelp = GetCommandOptions(options);
                    if (!string.IsNullOrWhiteSpace(optionsHelp))
                    {
                        embed.AddField("Options", optionsHelp);
                    }
                }
            }
            
            await result.Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public static async Task SendCommandInfo(CommandInfo command, ICommandContext context, string prefix)
        {
            var str = $"**`{prefix + command.Aliases[0]}`**";
            string aliases = string.Join("/", command.Aliases.Skip(1).Select(a => $"`{a}`"));
            if (!string.IsNullOrWhiteSpace(aliases))
            {
                str += $"/{aliases}";
            }
            
            string[] remarks = command.Remarks.Deserialize<string[]>().Select(x => string.Format(x, prefix)).ToArray();
            EmbedBuilder embed = new EmbedBuilder().WithTitle(str)
                .WithDescription(command.FormatSummary(prefix))
                .AddField("Usage", $"```{remarks[0]}```")
                .WithFooter($"Module: {command.Module.GetTopLevelModule().Name}");

            if (remarks.Length > 1)
            {
                embed.AddField("Examples", $"```{string.Join('\n', remarks[1..])}```");
            }

            if (context.Channel is IDMChannel)
            {
                embed.WithOkColor();
            }
            else
            {
                embed.WithDynamicColor(context);
            }

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
            var helpOptions = new StringBuilder();
            helpOptions.Append("```");
            foreach (object attribute in option.GetProperties()
                .Select(x => x.GetCustomAttributes(true).FirstOrDefault(a => a is OptionAttribute || a is ValueAttribute))
                .Where(x => x != null))
            {
                if (attribute is OptionAttribute optionAttribute)
                {
                    if (string.IsNullOrWhiteSpace(optionAttribute.ShortName))
                    {
                        helpOptions.Append($"--{optionAttribute.LongName} {optionAttribute.MetaValue}").Append("\n\t").AppendLine(optionAttribute.HelpText);
                    }
                    else
                    {
                        helpOptions.Append($"--{optionAttribute.ShortName}, --{optionAttribute.LongName} {optionAttribute.MetaValue}").Append("\n\t").AppendLine(optionAttribute.HelpText);
                    }
                }
                else if (attribute is ValueAttribute valueAttribute)
                {
                    helpOptions.Append($"{valueAttribute.MetaValue}").Append("\n\t").AppendLine(valueAttribute.HelpText);
                }
            }
            helpOptions.Append("```");
            return helpOptions.ToString();
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