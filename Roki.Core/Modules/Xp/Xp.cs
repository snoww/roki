using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Xp.Common;

namespace Roki.Modules.Xp
{
    public partial class Xp : RokiTopLevelModule
    {
        private readonly DbService _db;


        public Xp(DbService db)
        {
            _db = db;
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Experience([Leftover] IUser user = null)
        {
            user ??= ctx.User;
            using var uow = _db.GetDbContext();
            var dUser = await uow.DUsers.GetOrCreate(user).ConfigureAwait(false);
            var xp = new XpLevel(dUser.TotalXp);
            await ctx.Channel.SendMessageAsync($"{user.Username}\nLevel: `{xp.Level:N0}`\nTotal XP: `{xp.TotalXp:N0}`\nXP Progress: `{xp.LevelXp:N0}`/`{xp.RequiredXp:N0}`");
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task XpLeaderboard([Leftover] int page = 0)
        {
            if (page < 0)
                return;
            if (page > 0)
                page -= 1;
            using (var uow = _db.GetDbContext())
            {
                var list = uow.DUsers.GetUsersXpLeaderboard(page);
                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle("XP Leaderboard");
                var i = 9 * page + 1;
                foreach (var user in list)
                {
                    embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"Level `{new XpLevel(user.TotalXp).Level:N0}` - `{user.TotalXp:N0}` xp");
                }

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task XpNotification(string notify = null, IUser user = null)
        {
            var caller = ctx.User as SocketGuildUser;
            var isAdmin = false;
            if (user != null && !caller.GuildPermissions.Administrator)
                return;
            if (user != null && caller.GuildPermissions.Administrator)
                isAdmin = true;
            
            if (string.IsNullOrWhiteSpace(notify))
            {
                await ctx.Channel
                    .EmbedAsync(new EmbedBuilder().WithErrorColor().WithDescription("Please provide notification location for xp: none, dm, server."))
                    .ConfigureAwait(false);
                return;
            }

            if (!notify.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                !notify.Equals("dm", StringComparison.OrdinalIgnoreCase) ||
                !notify.Equals("server", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel
                    .EmbedAsync(new EmbedBuilder().WithErrorColor().WithDescription("Not a valid option. Options are none, dm, or server."))
                    .ConfigureAwait(false);
                return;
            }

            using var uow = _db.GetDbContext();
            if (!isAdmin)
                await uow.DUsers.ChangeNotificationLocation(ctx.User.Id, notify).ConfigureAwait(false);
            else
                await uow.DUsers.ChangeNotificationLocation(user.Id, notify).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription("Successfully changed xp notification preferences."))
                .ConfigureAwait(false);
        }
    }
}