using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Moderation.Services;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Moderation
{
    public partial class Moderation
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]

        public class ConfigCommands : RokiSubmodule<ConfigService>
        {
            private readonly IConfigurationService _config;

            public ConfigCommands(IConfigurationService config)
            {
                _config = config;
            }


            [RokiCommand, Description, Usage, Aliases]
            public async Task GuildConfig(ConfigCategory category = ConfigCategory.Settings)
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                EmbedBuilder builder = ConfigService.PrintGuildConfig(category, guildConfig);

                await Context.Channel.EmbedAsync(builder.WithDynamicColor(Context)
                    .WithTitle($"Server Configuration Settings - {category.ToString()}")).ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task ChannelConfig()
            {
                ChannelConfig channelConfig = await _config.GetChannelConfigAsync(Context.Channel as ITextChannel);
                EmbedBuilder builder = ConfigService.PrintChannelConfig(channelConfig);

                await Context.Channel.EmbedAsync(builder.WithDynamicColor(Context)
                    .WithTitle($"Channel Configuration Settings - {Context.Channel.Name}")).ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Prefix(string prefix = "")
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"The prefix for this server is `{guildConfig.Prefix}`\nTo change prefix: `{guildConfig.Prefix}prefix <new_prefix>`\nTo reset to default: `{guildConfig.Prefix}prefix default`"));
                }
                else if (prefix.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    guildConfig.Prefix = Roki.Properties.Prefix;
                    await _config.UpdatePrefix(Context.Guild.Id, Roki.Properties.Prefix);
                    await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"The prefix for this server is now set to `{guildConfig.Prefix}`"));
                }
                else if (prefix.Length > 5)
                {
                    await Context.Channel.SendErrorAsync("The maximum length for prefix is 5 characters.");
                }
                else if (guildConfig.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"The prefix for this server already is `{guildConfig.Prefix}`\nTo change prefix: `{guildConfig.Prefix}prefix <new_prefix>`\nTo reset to default: `{guildConfig.Prefix}prefix default`"));
                }
                else
                {
                    guildConfig.Prefix = prefix;
                    await _config.UpdatePrefix(Context.Guild.Id, prefix);
                    await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"The prefix for this server is now set to `{guildConfig.Prefix}`"));
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task ServerLogging(string option = "")
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                if (option.Equals("enable", StringComparison.OrdinalIgnoreCase))
                {
                    if (guildConfig.Logging)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server Logging is already **enabled**. All new channels created will have logging **enabled** by default."));
                    }
                    else
                    {
                        guildConfig.Logging = true;
                        await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server Logging is **enabled**. All new channels created will have logging **enabled** by default."));
                    }
                }
                else if (option.Equals("disable", StringComparison.OrdinalIgnoreCase))
                {
                    if (guildConfig.Logging)
                    {
                        guildConfig.Logging = false;
                        await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server Logging is **disabled**. All new channels created will have logging **disabled** by default."));
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server Logging is already **disabled**. All new channels created will have logging **disabled** by default."));
                    }
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription(string.Format("Server Logging is **{0}**. All new channels created will have logging **{0}** by default.", guildConfig.Logging ? "enabled" : "disabled")));
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task ServerCurrencyGeneration(string option = "")
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                if (option.Equals("enable", StringComparison.OrdinalIgnoreCase))
                {
                    if (guildConfig.CurrencyGeneration)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server CurrencyGeneration is already **enabled**. All new channels created will have CurrencyGeneration **enabled** by default."));
                    }
                    else
                    {
                        guildConfig.CurrencyGeneration = true;
                        await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server CurrencyGeneration is **enabled**. All new channels created will have CurrencyGeneration **enabled** by default."));
                    }
                }
                else if (option.Equals("disable", StringComparison.OrdinalIgnoreCase))
                {
                    if (guildConfig.CurrencyGeneration)
                    {
                        guildConfig.CurrencyGeneration = false;
                        await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server CurrencyGeneration is **disabled**. All new channels created will have CurrencyGeneration **disabled** by default."));
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server CurrencyGeneration is already **disabled**. All new channels created will have CurrencyGeneration **disabled** by default."));
                    }
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription(string.Format("Server CurrencyGeneration is **{0}**. All new channels created will have CurrencyGeneration **{0}** by default.", guildConfig.Logging ? "enabled" : "disabled")));
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task ServerXpGain(string option = "")
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                if (option.Equals("enable", StringComparison.OrdinalIgnoreCase))
                {
                    if (guildConfig.XpGain)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server XpGain is already **enabled**. All new channels created will have XpGain **enabled** by default."));
                    }
                    else
                    {
                        guildConfig.XpGain = true;
                        await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server XpGain is **enabled**. All new channels created will have XpGain **enabled** by default."));
                    }
                }
                else if (option.Equals("disable", StringComparison.OrdinalIgnoreCase))
                {
                    if (guildConfig.XpGain)
                    {
                        guildConfig.XpGain = false;
                        await _config.UpdateGuildConfigAsync(Context.Guild.Id, guildConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server XpGain is **disabled**. All new channels created will have XpGain **disabled** by default."));
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Server XpGain is already **disabled**. All new channels created will have XpGain **disabled** by default."));
                    }
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription(string.Format("Server XpGain is **{0}**. All new channels created will have XpGain **{0}** by default.", guildConfig.Logging ? "enabled" : "disabled")));
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Logging(string option = "", ITextChannel channel = null)
            {
                ChannelConfig channelConfig = await _config.GetChannelConfigAsync(channel??Context.Channel as ITextChannel);
                if (option.Equals("enable", StringComparison.OrdinalIgnoreCase))
                {
                    if (channelConfig.Logging)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Logging is already **enabled** in this channel."));
                    }
                    else
                    {
                        channelConfig.Logging = true;
                        await _config.UpdateLogging(Context.Channel.Id, true);
                        await _config.UpdateChannelConfigAsync(Context.Channel as ITextChannel, channelConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Logging is now **enabled** in this channel. All messages will be logged."));
                    }
                }
                else if (option.Equals("disable", StringComparison.OrdinalIgnoreCase))
                {
                    if (channelConfig.Logging)
                    {
                        channelConfig.Logging = false;
                        await _config.UpdateLogging(Context.Channel.Id, false);
                        await _config.UpdateChannelConfigAsync(Context.Channel as ITextChannel, channelConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Logging is now **disabled** in this channel."));
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("Logging is already **disabled** in this channel."));
                    }
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"Logging is **{(channelConfig.Logging ? "enabled" : "disabled")}** in this channel.\n" +
                                         $"To change: `{await _config.GetGuildPrefix(Context.Guild.Id)}logging enable/disable`"));
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task CurrencyGeneration(string option = "", ITextChannel channel = null)
            {
                ChannelConfig channelConfig = await _config.GetChannelConfigAsync(channel??Context.Channel as ITextChannel);
                if (option.Equals("enable", StringComparison.OrdinalIgnoreCase))
                {
                    if (channelConfig.CurrencyGeneration)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("CurrencyGeneration is already **enabled** in this channel."));
                    }
                    else
                    {
                        channelConfig.CurrencyGeneration = true;
                        await _config.UpdateCurGen(Context.Channel.Id, true);
                        await _config.UpdateChannelConfigAsync(Context.Channel as ITextChannel, channelConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("CurrencyGeneration is now **enabled** in this channel."));
                    }
                }
                else if (option.Equals("disable", StringComparison.OrdinalIgnoreCase))
                {
                    if (channelConfig.CurrencyGeneration)
                    {
                        channelConfig.CurrencyGeneration = false;
                        await _config.UpdateCurGen(Context.Channel.Id, false);
                        await _config.UpdateChannelConfigAsync(Context.Channel as ITextChannel, channelConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("CurrencyGeneration is now **disabled** in this channel."));
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("CurrencyGeneration is already **disabled** in this channel."));
                    }
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"CurrencyGeneration is **{(channelConfig.CurrencyGeneration ? "enabled" : "disabled")}** in this channel.\n" +
                                         $"To change: `{await _config.GetGuildPrefix(Context.Guild.Id)}currencygeneration enable/disable`"));
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task XpGain(string option = "", ITextChannel channel = null)
            {
                ChannelConfig channelConfig = await _config.GetChannelConfigAsync(channel??Context.Channel as ITextChannel);
                if (option.Equals("enable", StringComparison.OrdinalIgnoreCase))
                {
                    if (channelConfig.XpGain)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("XpGain is already **enabled** in this channel."));
                    }
                    else
                    {
                        channelConfig.XpGain = true;
                        await _config.UpdateXpGain(Context.Channel.Id, true);
                        await _config.UpdateChannelConfigAsync(Context.Channel as ITextChannel, channelConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("XpGain is now **enabled** in this channel."));
                    }
                }
                else if (option.Equals("disable", StringComparison.OrdinalIgnoreCase))
                {
                    if (channelConfig.XpGain)
                    {
                        channelConfig.XpGain = false;
                        await _config.UpdateXpGain(Context.Channel.Id, false);
                        await _config.UpdateChannelConfigAsync(Context.Channel as ITextChannel, channelConfig);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("XpGain is now **disabled** in this channel."));
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription("XpGain is already **disabled** in this channel."));
                    }
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"XpGain is **{(channelConfig.XpGain ? "enabled" : "disabled")}** in this channel.\n" +
                                         $"To change: `{await _config.GetGuildPrefix(Context.Guild.Id)}xpgain enable/disable`"));
                }
            }
        }
    }
}