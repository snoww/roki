using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Utility
{
    public partial class Utility
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

            [RokiCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild)]
            public async Task ServerInfo(string guildName = null)
            {
                var channel = (ITextChannel) ctx.Channel;
                guildName = guildName?.ToUpperInvariant();
                SocketGuild guild;
                if (string.IsNullOrWhiteSpace(guildName))
                    guild = (SocketGuild) channel.Guild;
                else
                    guild = _client.Guilds.FirstOrDefault(g => string.Equals(g.Name, guildName, StringComparison.InvariantCultureIgnoreCase));
                if (guild == null)
                    return;

                var ownerName = guild.GetUser(guild.OwnerId);
                var textChannels = guild.TextChannels.Count;
                var voiceChannels = guild.VoiceChannels.Count;

                var createdOn = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(guild.Id >> 22);
                var features = string.Join("\n", guild.Features);

                if (string.IsNullOrWhiteSpace(features))
                    features = "-";

                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor("Server Info")
                    .WithTitle(guild.Name)
                    .AddField("ID", guild.Id, true)
                    .AddField("Owner", ownerName.ToString(), true)
                    .AddField("Members", guild.MemberCount, true)
                    .AddField("Text Channels", textChannels, true)
                    .AddField("Voice Channels", voiceChannels, true)
                    .AddField("Created on", $"{createdOn:MM/dd/yyyy HH:mm}", true)
                    .AddField("Region", guild.VoiceRegionId, true)
                    .AddField("Roles", guild.Roles.Count - 1, true)
                    .AddField("Features", features, true);
                if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
                    embed.WithThumbnailUrl(guild.IconUrl);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task ChannelInfo(ITextChannel channel = null)
            {
                var ch = channel ?? (ITextChannel) ctx.Channel;
                if (ch == null)
                    return;

                var createdOn = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ch.Id >> 22);
                var userCount = (await ch.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count();

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(ch.Name)
                    .WithDescription(ch.Topic)
                    .AddField("ID", ch.Id, true)
                    .AddField("Created on", $"{createdOn:MM/dd/yyyy HH:mm}", true)
                    .AddField("Users", userCount, true);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task UserInfo(IGuildUser user = null)
            {
                var usr = user ?? ctx.User as IGuildUser;
                if (usr == null)
                    return;

                var embed = new EmbedBuilder().WithOkColor()
                    .AddField("Name", $"**{usr.Username}**#{usr.Discriminator}", true);

                if (!string.IsNullOrWhiteSpace(usr.Nickname))
                    embed.AddField("Nickname", usr.Nickname, true);

                embed.AddField("ID", usr.Id, true)
                    .AddField("Joined server", $"{usr.JoinedAt?.ToString("MM/dd/yyyy HH:mm") ?? "?"}", true)
                    .AddField("Joined Discord", $"{usr.CreatedAt:MM/dd/yyyy HH:mm}", true)
                    .AddField("Roles",
                        $"**({usr.RoleIds.Count - 1})** - {string.Join("\n", usr.GetRoles().Take(10).Where(r => r.Id != r.Guild.EveryoneRole.Id).Select(r => r.Name))}",
                        true);

                var avatar = usr.RealAvatarUrl();
                if (avatar != null && avatar.IsAbsoluteUri)
                    embed.WithThumbnailUrl(avatar.ToString());

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task Stalk([Leftover] string username)
            {
                var client = (IDiscordClient) _client;
                var usr = username.Split("#");
                var user = await client.GetUserAsync(usr[0], usr[1]).ConfigureAwait(false);
                if (user == null)
                {
                    await ctx.Channel.SendErrorAsync("No user found with that ID").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor()
                    .AddField("Name", $"**{user.Username}**#{user.Discriminator}", true)
                    .AddField("Joined Discord", $"{user.CreatedAt:MM/dd/yyyy HH:mm}", true);

                var avatar = user.RealAvatarUrl();
                if (avatar != null && avatar.IsAbsoluteUri)
                    embed.WithThumbnailUrl(avatar.ToString());

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task Avatar([Leftover] IGuildUser user = null)
            {
                if (user == null)
                    user = (IGuildUser) ctx.User;

                var avatarUrl = user.RealAvatarUrl();

                if (avatarUrl == null)
                {
                    await ctx.Channel.SendErrorAsync($"{user} does not have an avatar set").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor()
                    .WithThumbnailUrl(avatarUrl.ToString())
                    .WithImageUrl(avatarUrl.ToString())
                    .AddField("Username", user.ToString(), true)
                    .AddField("Avatar Url", avatarUrl, true);

                await ctx.Channel.EmbedAsync(embed, ctx.User.Mention).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Ping()
            {
                var sw = Stopwatch.StartNew();
                var msg = await ctx.Channel.SendMessageAsync("🏓").ConfigureAwait(false);
                sw.Stop();
                msg.DeleteAfter(0);

                var embed = new EmbedBuilder();
                embed.WithOkColor()
                    .WithAuthor("Pong! 🏓")
                    .WithDescription($"Currently {(int) sw.Elapsed.TotalMilliseconds}ms");

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}