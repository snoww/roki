using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Moderation.Services;
using Roki.Services;
using Roki.Services.Database.Models;

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
                ChannelConfig channelConfig = await _config.GetChannelConfigAsync(Context.Channel.Id);
                EmbedBuilder builder = ConfigService.PrintChannelConfig(channelConfig);

                await Context.Channel.EmbedAsync(builder.WithDynamicColor(Context)
                    .WithTitle($"Channel Configuration Settings - {Context.Channel.Name}")).ConfigureAwait(false);
            }
        }
    }
}