using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Moderation.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Moderation
{
    public partial class Moderation
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class ConfigCommands : RokiSubmodule<ConfigService>
        {
            [RokiCommand, Description, Usage, Aliases]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task GuildConfig(ConfigCategory category = ConfigCategory.All)
            {
                GuildConfig guildConfig = await Service.GetGuildConfigAsync(Context.Guild.Id);
                EmbedBuilder builder = ConfigService.PrintGuildConfig(category, guildConfig);

                await Context.Channel.EmbedAsync(builder.WithDynamicColor(Context)
                    .WithTitle($"Server Configuration Settings - {category.ToString()}")).ConfigureAwait(false);
            }
        
            [RokiCommand, Description, Usage, Aliases]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ChannelConfig()
            {
                ChannelConfig channelConfig = await Service.GetChannelConfigAsync(Context.Channel as ITextChannel);
                EmbedBuilder builder = ConfigService.PrintChannelConfig(channelConfig);

                await Context.Channel.EmbedAsync(builder.WithDynamicColor(Context)
                    .WithTitle($"Channel Configuration Settings - {Context.Channel.Name}")).ConfigureAwait(false);
            }
        }
    }
}