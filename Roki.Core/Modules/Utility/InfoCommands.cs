using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class InfoCommands : RokiSubmodule
        {
            private readonly DiscordSocketClient _client;
            private readonly IRokiConfig _config;
            private readonly IStatsService _stats;

            public InfoCommands(DiscordSocketClient client, IStatsService stats, IRokiConfig config)
            {
                _client = client;
                _stats = stats;
                _config = config;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Invite()
            {
                // todo mabye link to web interface
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Roki Invite Link")
                        .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                        .WithDescription($"https://discord.com/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&permissions=3724016759&scope=bot"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task About()
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle($"Roki v{StatsService.BotVersion}")
                        .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl() ?? Context.Client.CurrentUser.GetDefaultAvatarUrl())
                        .WithDescription($"Roki `/ˈräkē/` is a general purpose discord bot for small communities, made by {_stats.AuthorUsername}. The bot is open source on [GitHub](https://github.com/snoww/roki).\n\n" +
                                         $"Written in C# using Discord.Net v{DiscordConfig.Version} and inspired by NadekoBot and YAGPDB.\n\n" +
                                         "Roki has many features, including but not limited to: XP system, Currency system, Event system, Jeopardy!, and many more. " +
                                         $"If you have any questions/issues, feel free to send a DM to {_stats.Author}"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Stats()
            {
                await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithDynamicColor(Context)
                        .WithAuthor($"Roki v{StatsService.BotVersion}")
                        .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl() ?? Context.Client.CurrentUser.GetDefaultAvatarUrl())
                        .AddField("Bot Name", _client.CurrentUser, true)
                        .AddField("Owner", _stats.AuthorUsername, true)
                        .AddField("Memory", $"{_stats.Heap} MB", true)
                        .AddField("Messages", _stats.MessageCounter, true)
                        .AddField("Commands ran", _stats.CommandsRan, true)
                        .AddField("Uptime", _stats.GetUptimeString(", "))
                        .AddField("Presence", $"{_stats.TextChannels:N0} Text Channels\n{_stats.Guilds:N0} Servers"));
            }

            [RokiCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild)]
            public async Task ServerInfo(string guildName = null)
            {
                var channel = (ITextChannel) Context.Channel;
                guildName = guildName?.ToUpperInvariant();
                SocketGuild guild;
                if (string.IsNullOrWhiteSpace(guildName))
                {
                    guild = (SocketGuild) channel.Guild;
                }
                else
                {
                    guild = _client.Guilds.FirstOrDefault(g => string.Equals(g.Name, guildName, StringComparison.InvariantCultureIgnoreCase));
                }

                if (guild == null)
                {
                    return;
                }

                SocketGuildUser ownerName = guild.GetUser(guild.OwnerId);
                int textChannels = guild.TextChannels.Count;
                int voiceChannels = guild.VoiceChannels.Count;

                string features = string.Join("\n", guild.Features);

                if (string.IsNullOrWhiteSpace(features))
                {
                    features = "-";
                }

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithAuthor("Server Information")
                    .WithTitle(guild.Name)
                    .AddField("Server ID", guild.Id, true)
                    .AddField("Server Owner", ownerName.ToString(), true)
                    .AddField("Members", guild.MemberCount, true)
                    .AddField("Text Channels", textChannels, true)
                    .AddField("Voice Channels", voiceChannels, true)
                    .AddField("Created on", $"{guild.CreatedAt:yyyy-MM-dd HH:mm} UTC", true)
                    .AddField("Region", guild.VoiceRegionId, true)
                    .AddField("Roles", guild.Roles.Count - 1, true)
                    .AddField("Features", features, true);
                if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
                {
                    embed.WithThumbnailUrl(guild.IconUrl);
                }

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task ChannelInfo(ITextChannel channel = null)
            {
                channel ??= (ITextChannel) Context.Channel;
                if (channel == null)
                {
                    return;
                }

                int userCount = (await channel.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count();

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle(channel.Name)
                    .WithDescription(channel.Topic)
                    .AddField("ID", channel.Id)
                    .AddField("Created on", $"{channel.CreatedAt:yyyy-MM-dd HH:mm} UTC")
                    .AddField("Users", userCount);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task UserInfo(IGuildUser user = null)
            {
                IGuildUser usr = user ?? Context.User as IGuildUser;
                if (usr == null)
                {
                    return;
                }

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .AddField("Name", user, true);

                if (!string.IsNullOrWhiteSpace(usr.Nickname))
                {
                    embed.AddField("Nickname", usr.Nickname, true);
                }

                embed.AddField("ID", usr.Id)
                    .AddField("Joined server", $"{usr.JoinedAt?.ToString("yyyy-MM-dd HH:mm HH:mm") ?? "unknown"} UTC")
                    .AddField("Joined Discord", $"{usr.CreatedAt:yyyy-MM-dd HH:mm} UTC")
                    .AddField("Roles", $"{string.Join("\n", usr.GetRoles().Take(10).Where(r => r.Id != r.Guild.EveryoneRole.Id).Select(r => r.Mention))}", true);

                string avatar = usr.GetAvatarUrl();
                if (avatar != null)
                {
                    embed.WithThumbnailUrl(avatar);
                }

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task Avatar([Leftover] IGuildUser user = null)
            {
                user ??= (IGuildUser) Context.User;

                string avatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithThumbnailUrl(avatarUrl)
                    .WithTitle($"{user}'s Avatar")
                    .AddField("Link", avatarUrl);

                await Context.Channel.EmbedAsync(embed, Context.User.Mention).ConfigureAwait(false);
            }
        }
    }
}