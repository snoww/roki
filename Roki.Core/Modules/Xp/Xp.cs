using System.Threading.Tasks;
using Discord;
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
            user = user ?? ctx.User;
            using (var uow = _db.GetDbContext())
            {
                var dUser = uow.DUsers.GetOrCreate(user);
                var xp = new XpLevel(dUser.TotalXp);
                await ctx.Channel.SendMessageAsync($"{user.Username}\nLevel: {xp.Level}\nTotal XP: {xp.TotalXp}\nXP Progress: {xp.LevelXp}/{xp.RequiredXp}");
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task XpLeaderboard([Leftover] int page = 0)
        {
            if (page < 0)
                return;
            using (var uow = _db.GetDbContext())
            {
                var list = uow.DUsers.GetUsersXpLeaderboard(page);
                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle("XP Leaderboard");
                var i = 9 * page + 1;
                foreach (var user in list)
                {
                    embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"Level {new XpLevel(user.TotalXp).Level} - {user.TotalXp}xp");
                }

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}