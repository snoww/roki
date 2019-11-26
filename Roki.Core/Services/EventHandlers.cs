using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services
{
    public class EventHandlers
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;

        public EventHandlers(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
            _client.UserJoined += UserJoined;
            _client.UserUpdated += UserUpdated;
            _client.JoinedGuild += JoinedGuild;
            _client.GuildUpdated += GuildUpdated;
            _client.ChannelCreated += ChannelCreated;
            _client.ChannelUpdated += ChannelUpdated;
        }

        private Task GuildUpdated(SocketGuild before, SocketGuild after)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var guild = await uow.Context.Guilds.FirstAsync(g => g.GuildId == before.Id).ConfigureAwait(false);
                guild.Name = after.Name;
                guild.ChannelCount = after.Channels.Count;
                guild.EmoteCount = after.Emotes.Count;
                guild.IconId = after.IconId;
                guild.MemberCount = after.MemberCount;
                guild.RegionId = after.VoiceRegionId;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (!(before is SocketGuildChannel guildChannel)) return Task.CompletedTask;
            if (guildChannel is SocketTextChannel textChannel)
            {
                var _ = Task.Run(async () =>
                {
                    using var uow = _db.GetDbContext();
                    var channel = await uow.Channels.GetOrCreateChannelAsync(textChannel).ConfigureAwait(false);
                    channel.Name = textChannel.Name;
                    channel.GuildName = textChannel.Guild.Name;
                    channel.UserCount = textChannel.Users.Count;
                    channel.IsNsfw = textChannel.IsNsfw;
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                });
            }
            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel channel)
        {
            if (!(channel is SocketGuildChannel guildChannel)) return Task.CompletedTask;
            if (guildChannel is SocketTextChannel textChannel)
            {
                var _ = Task.Run(async () =>
                {
                    using var uow = _db.GetDbContext();
                    await uow.Channels.GetOrCreateChannelAsync(textChannel).ConfigureAwait(false);
                });
            }
            return Task.CompletedTask;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                await uow.Guilds.GetOrCreateGuildAsync(guild).ConfigureAwait(false);
                await guild.DownloadUsersAsync().ConfigureAwait(false);
                var users = guild.Users;
                foreach (var user in users)
                {
                    await uow.Users.GetOrCreateUserAsync(user);
                }
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var user = await uow.Context.Users.FirstAsync(u => u.UserId == before.Id).ConfigureAwait(false);
                user.Username = after.Username;
                user.Discriminator = after.Discriminator;
                user.AvatarId = after.AvatarId;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                await uow.Users.GetOrCreateUserAsync(user).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }
    }
}