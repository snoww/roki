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
            string errorMessage;
            var showUsage = true;
            Console.WriteLine(result.Result.Error);
            switch (result.Result.Error)
            {
                case CommandError.UnmetPrecondition:
                    showUsage = false;
                    if (result.Result.ErrorReason.Contains("context", StringComparison.Ordinal))
                    {
                        errorMessage = "This command can only be ran in Servers";
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
                await SendErrorInfo(result, Roki.Properties.Prefix, errorMessage, showUsage);
            }
            else
            {
                await SendErrorInfo(result, await _config.GetGuildPrefix(result.Context.Guild.Id), errorMessage, showUsage);
            }
        }
        
        private static async Task SendErrorInfo(ExecuteCommandResult result, string prefix, string customError, bool showUsage)
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
                embed.AddField("Correct Usage", "```" + result.CommandInfo.FormatRemarks(prefix) + "```");
            }

            Type options = ((RokiOptions) result.CommandInfo.Attributes.FirstOrDefault(x => x is RokiOptions))?.OptionType;
            if (options != null)
            {
                string optionsHelp = GetCommandOptions(options);
                if (!string.IsNullOrWhiteSpace(optionsHelp))
                {
                    embed.AddField("Options", optionsHelp);
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

            EmbedBuilder embed = new EmbedBuilder().WithTitle(str)
                .WithDescription(command.FormatSummary(prefix))
                .AddField("Examples", "```" + command.FormatRemarks(prefix) + "```")
                .WithFooter($"Module: {command.Module.GetTopLevelModule().Name}");
            
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