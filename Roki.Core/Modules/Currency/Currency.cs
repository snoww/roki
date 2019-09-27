using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Currency
{
    public partial class Currency : RokiTopLevelModule
    {
        private readonly DbService _db;

        public Currency(DbService db)
        {
            _db = db;
        }

        public async Task Leaderboard([Leftover] int page = 0)
        {
            if (page < 0)
                return;
            if (page > 0)
                page -= 1;
            using (var uow = _db.GetDbContext())
            {
                var list = uow.DUsers.GetCurrencyLeaderboard(page);
                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle("Currency Leaderboard");
                var i = 9 * page + 1;
                foreach (var user in list)
                {
                    embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", user.Currency);
                }

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}