using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Help.Services
{
    public class HelpService : IRokiService
    {
        private readonly CommandHandler _command;

        public HelpService(CommandHandler command)
        {
            _command = command;
        }

        public EmbedBuilder GetCommandInfo(CommandInfo command)
        {
            var prefix = _command.DefaultPrefix;

            var str = $"**`{prefix + command.Aliases.First()}`**";
            var aliases = string.Join("/", command.Aliases.Skip(1).Select(a => $"`{a}`"));
            if (!string.IsNullOrWhiteSpace(aliases)) 
                str += $"/{aliases}";
            
            var embed = new EmbedBuilder().WithOkColor().AddField(str, command.FormatSummary(prefix), true)
                .AddField("Usage", command.FormatRemarks(prefix), true)
                .WithFooter($"Module: {command.Module.GetTopLevelModule().Name}");;

            var requirements = GetCommandRequirements(command);
            if (!string.IsNullOrWhiteSpace(requirements)) 
                embed.AddField("Requires", requirements);

            var options = ((RokiOptions) command.Attributes.FirstOrDefault(x => x is RokiOptions))?.OptionType;
            if (options != null)
            {
                var optionsHelp = GetCommandOptions(options);
                if (!string.IsNullOrWhiteSpace(optionsHelp))
                    embed.AddField("Options", optionsHelp);
            }

            return embed;
        }

        private static string GetCommandOptions(Type option)
        {
            var options = option.GetProperties()
                .Select(x => x.GetCustomAttributes(true).FirstOrDefault(a => a is OptionAttribute))
                .Where(x => x != null)
                .Cast<OptionAttribute>()
                .Select(x =>
                {
                    var optionString = string.Empty; 
                        

                    if (!string.IsNullOrWhiteSpace(x.ShortName))
                        optionString += $"`-{x.ShortName}`, `--{x.LongName}`";
                    else
                        optionString += $"`--{x.LongName}`";
                    
                    optionString += $" {x.HelpText}";
                    return optionString;
                });
            return string.Join("\n", options);
        }

        private static string GetCommandRequirements(CommandInfo command)
        {
            var requirements = command.Preconditions
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