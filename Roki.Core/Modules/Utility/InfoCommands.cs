using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extentions;

namespace Roki.Modules.Utility
{
    public partial class Utiltiy
    {
        [Group]
        public class InfoCommands : RokiSubmodule
        {
            private readonly DiscordSocketClient _client;
            private readonly IStatsService _stats;

            public InfoCommands(DiscordSocketClient client, IStatsService stats)
            {
                _client = client;
                _stats = stats;
            }
            
            [RokiCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ServerInfo(string guildName = null)
            {
                var channel = (ITextChannel) ctx.Channel;
                guildName = guildName?.ToUpperInvariant();
                SocketGuild guild;
                if (string.IsNullOrWhiteSpace(guildName))
                    guild = (SocketGuild) channel.Guild;
                else
                    guild = _client.Guilds.FirstOrDefault(g => g.Name.ToUpperInvariant() == guildName.ToUpperInvariant());
                if (guild == null)
                    return;

                var ownerName = guild.GetUser(guild.OwnerId);
                var textChannels = guild.TextChannels.Count();
                var voiceChannels = guild.VoiceChannels.Count();
                
                var createdOn = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(guild.Id >> 22);
                var features = string.Join("\n", guild.Features);

                if (string.IsNullOrWhiteSpace(features))
                    features = "-";
                
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName("Server Info"))
                    .WithTitle(guild.Name)
                    .AddField(efb => efb.WithName("ID").WithValue(guild.Id.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Owner").WithValue(ownerName.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Members").WithValue(guild.MemberCount.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Text Channels").WithValue(textChannels.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Voice Channels").WithValue(voiceChannels.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Created on").WithValue($"{createdOn:MM/dd/yyyy HH:mm}").WithIsInline(true))
                    .AddField(efb => efb.WithName("Region").WithValue(guild.VoiceRegionId.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Roles").WithValue((guild.Roles.Count - 1).ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Features").WithValue(features).WithIsInline(true));
                if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
                    embed.WithThumbnailUrl(guild.IconUrl);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChannelInfo(ITextChannel channel = null)
            {
                var ch = channel ?? (ITextChannel)ctx.Channel;
                if (ch == null)
                    return;

                var createdOn = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ch.Id >> 22);
                var userCount = (await ch.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count();

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(ch.Name)
                    .WithDescription(ch.Topic)
                    .AddField(efb => efb.WithName("ID").WithValue(ch.Id.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Created on").WithValue($"{createdOn:MM/dd/yyyy HH:mm}").WithIsInline(true))
                    .AddField(efb => efb.WithName("Users").WithValue(userCount.ToString()).WithIsInline(true));

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task UserInfo(IGuildUser user = null)
            {
                var usr = user ?? ctx.User as IGuildUser;
                if (user == null)
                    return;

                var embed = new EmbedBuilder().WithOkColor()
                    .AddField(efb => efb.WithName("Name").WithValue($"**{usr.Username}**#{usr.Discriminator}").WithIsInline(true));
                if (!string.IsNullOrWhiteSpace(usr.Nickname))
                    embed.AddField(efb => efb.WithName("Nickname").WithValue(usr.Nickname).WithIsInline(true));
                embed.AddField(efb => efb.WithName("ID").WithValue(usr.Id).WithIsInline(true))
                    .AddField(efb => efb.WithName("Joined server").WithValue($"{usr.JoinedAt?.ToString("MM/dd/yyyy HH:mm") ?? "?"}").WithIsInline(true))
                    .AddField(efb => efb.WithName("Joined Discord").WithValue($"{usr.CreatedAt:MM/dd/yyyy HH:mm}").WithIsInline(true));
                //.AddField(efb => efb.WithName("Roles").WithValue($"**({usr.RoleIds.Count - 1})** - {string.Join("\n", usr.GetRoles().Take(10).Where(r => r.Id != r.Guild.EveryoneRole.Id).Select(r => r.Name))}").WithIsInline(true));

                var avatar = usr.RealAvatarUrl();
                if (avatar != null && avatar.IsAbsoluteUri)
                    embed.WithThumbnailUrl(avatar.ToString());

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}