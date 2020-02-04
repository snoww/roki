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
            private readonly IStatsService _stats;
            private readonly IRokiConfig _config;

            public InfoCommands(DiscordSocketClient client, IStatsService stats, IRokiConfig config)
            {
                _client = client;
                _stats = stats;
                _config = config;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task About()
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithAuthor("Snow#7777")
                        .WithTitle($"Roki v{StatsService.BotVersion}")
                        .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                        .WithDescription($"Roki is an general purpose discord bot, made by {_stats.Author}. The bot is open source on [GitHub](https://github.com/snoww/roki). " +
                                         $"Written in C# using Discord.Net v{DiscordConfig.Version} and inspired by NadekoBot and yagpdb. " +
                                         "Roki has many features, including but not limited to: XP system, Currency system, Event system, Jeopardy!, and many more. " +
                                         $"If you have any questions/issues, feel free to send a DM {_stats.Author}"))
                    .ConfigureAwait(false);

            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Stats()
            {
                var ownerId = string.Join("\n", _config.OwnerIds);
                if (string.IsNullOrWhiteSpace(ownerId)) ownerId = "-";

                await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithDynamicColor(Context)
                        .WithAuthor($"Roki v{StatsService.BotVersion}", Context.Client.CurrentUser.GetAvatarUrl())
                        .AddField("Bot ID", _client.CurrentUser.Id, true)
                        .AddField("Owner ID", ownerId, true)
                        .AddField("Commands ran", _stats.CommandsRan, true)
                        .AddField("Messages", _stats.MessageCounter, true)
                        .AddField("Memory", $"{_stats.Heap} MB", true)
                        .AddField("Uptime", _stats.GetUptimeString("\n"), true)
                        .AddField("Presence", $"{_stats.TextChannels} Text Channels\n{_stats.VoiceChannels} Voice Channels", true));
            }

            [RokiCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild)]
            public async Task ServerInfo(string guildName = null)
            {
                var channel = (ITextChannel) Context.Channel;
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

                var features = string.Join("\n", guild.Features);

                if (string.IsNullOrWhiteSpace(features))
                    features = "-";

                var embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithAuthor("Server Information")
                    .WithTitle(guild.Name)
                    .AddField("Server ID", guild.Id, true)
                    .AddField("Server Owner", ownerName.ToString(), true)
                    .AddField("Members", guild.MemberCount, true)
                    .AddField("Text Channels", textChannels, true)
                    .AddField("Voice Channels", voiceChannels, true)
                    .AddField("Created on", $"{guild.CreatedAt:MM/dd/yyyy HH:mm}", true)
                    .AddField("Region", guild.VoiceRegionId, true)
                    .AddField("Roles", guild.Roles.Count - 1, true)
                    .AddField("Features", features, true);
                if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
                    embed.WithThumbnailUrl(guild.IconUrl);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task ChannelInfo(ITextChannel channel = null)
            {
                channel ??= (ITextChannel) Context.Channel;
                if (channel == null)
                    return;

                var userCount = (await channel.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count();

                var embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle(channel.Name)
                    .WithDescription(channel.Topic)
                    .AddField("ID", channel.Id, true)
                    .AddField("Created on", $"{channel.CreatedAt:MM/dd/yyyy HH:mm}", true)
                    .AddField("Users", userCount, true);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task UserInfo(IGuildUser user = null)
            {
                var usr = user ?? Context.User as IGuildUser;
                if (usr == null)
                    return;

                var embed = new EmbedBuilder().WithDynamicColor(Context)
                    .AddField("Name", $"**{usr.Username}**#{usr.Discriminator}", true);

                if (!string.IsNullOrWhiteSpace(usr.Nickname))
                    embed.AddField("Nickname", usr.Nickname, true);

                embed.AddField("ID", usr.Id, true)
                    .AddField("Joined server", $"{usr.JoinedAt?.ToString("MM/dd/yyyy HH:mm") ?? "?"}", true)
                    .AddField("Joined Discord", $"{usr.CreatedAt:MM/dd/yyyy HH:mm}", true)
                    .AddField("Roles", $"{string.Join("\n", usr.GetRoles().Take(10).Where(r => r.Id != r.Guild.EveryoneRole.Id).Select(r => r.Mention))}", true);

                var avatar = usr.GetAvatarUrl();
                if (avatar != null)
                    embed.WithThumbnailUrl(avatar);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task Stalk([Leftover] string username)
            {
                var client = (IDiscordClient) _client;
                var usr = username.Split("#");
                var user = await client.GetUserAsync(usr[0], usr[1]).ConfigureAwait(false);
                if (user == null)
                {
                    await Context.Channel.SendErrorAsync("No such user found").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithDynamicColor(Context)
                    .AddField("Name", $"**{user.Username}**#{user.Discriminator}", true)
                    .AddField("Joined Discord", $"{user.CreatedAt:MM/dd/yyyy HH:mm}", true);

                var avatar = user.GetAvatarUrl();
                if (avatar != null)
                    embed.WithThumbnailUrl(avatar);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task Avatar([Leftover] IGuildUser user = null)
            {
                if (user == null)
                    user = (IGuildUser) Context.User;

                var avatarUrl = user.GetAvatarUrl();

                if (avatarUrl == null)
                {
                    await Context.Channel.SendErrorAsync($"{user} does not have an avatar set").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithThumbnailUrl(avatarUrl)
                    .WithImageUrl(avatarUrl)
                    .AddField("Username", user.ToString(), true)
                    .AddField("Avatar Url", avatarUrl, true);

                await Context.Channel.EmbedAsync(embed, Context.User.Mention).ConfigureAwait(false);
            }
        }
    }
}