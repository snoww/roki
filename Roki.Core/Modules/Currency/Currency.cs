using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Currency
{
    public partial class Currency : RokiTopLevelModule
    {
        private readonly DbService _db;
        private readonly ICurrencyService _currency;

        public Currency(DbService db, ICurrencyService currency)
        {
            _db = db;
            _currency = currency;
        }

        private long GetCurrency(ulong userId)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.DUsers.GetUserCurrency(userId);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Cash([Leftover] IUser user = null)
        {
            user = user ?? ctx.User;
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"You have {GetCurrency(user.Id)} stones")).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Leaderboard([Leftover] int page = 1)
        {
            if (page <= 0)
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
                    embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"{user.Currency} <:stone:269130892100763649>");
                }

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Give(long amount, IGuildUser user, [Leftover] string message)
        {
            if (amount <= 0 || ctx.User.Id == user.Id || user.IsBot)
                return;
            message = $"Gift from {ctx.User.Username} - {message}";
            var success = await _currency.TransferAsync(ctx.User, user, message, amount, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id)
                .ConfigureAwait(false);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync("You do not have enough stones to give.").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"{ctx.User.Username} gifted {amount} stones to {user.Username}")).ConfigureAwait(false);
        }
    }
}