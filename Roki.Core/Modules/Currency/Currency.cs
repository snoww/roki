using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Currency
{
    public partial class Currency : RokiTopLevelModule
    {
        private readonly DbService _db;
        private readonly ICurrencyService _currency;
        private readonly Roki _roki;

        public Currency(DbService db, ICurrencyService currency, Roki roki)
        {
            _db = db;
            _currency = currency;
            _roki = roki;
        }

        private long GetCurrency(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.DUsers.GetUserCurrency(userId);
        }

        private decimal GetInvAccount(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.DUsers.GetUserInvestingAccount(userId);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Cash([Leftover] IUser user = null)
        {
            user ??= ctx.User;
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"{user.Mention}'s Accounts\nCash Account: `{GetCurrency(user.Id):N0}` {_roki.Properties.CurrencyIcon}\n" +
                                 $"Investing Account: `{GetInvAccount(user.Id):N}` {_roki.Properties.CurrencyIcon}")).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Leaderboard([Leftover] int page = 1)
        {
            if (page <= 0)
                return;
            if (page > 0)
                page -= 1;
            using var uow = _db.GetDbContext();
            var list = uow.DUsers.GetCurrencyLeaderboard(ctx.Client.CurrentUser.Id, page);
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle("Currency Leaderboard");
            var i = 9 * page + 1;
            foreach (var user in list)
            {
                embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"{user.Currency.FormatNumber()} {_roki.Properties.CurrencyIcon}");
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Transfer(Account account, long amount)
        {
            if (amount == 0)
            {
                await ctx.Channel.SendErrorAsync($"You must transfer at least `1` {_roki.Properties.CurrencyIcon}").ConfigureAwait(false);
                return;
            }

            using var uow = _db.GetDbContext();
            var success = false;
            var fromAcc = "";
            var toAcc = "";
            if ((int) account == 0)
            {
                success = await uow.DUsers.TransferToFromInvestingAccountAsync(ctx.User.Id, -amount).ConfigureAwait(false);
                fromAcc = "Investing Account";
                toAcc = "Cash Account";
            }
            else if ((int) account == 2)
            {
                success = await uow.DUsers.TransferToFromInvestingAccountAsync(ctx.User.Id, amount).ConfigureAwait(false);
                toAcc = "Investing Account";
                fromAcc = "Cash Account";
            }
            
            uow.Transaction.Add(new CurrencyTransaction
            {
                Amount = amount,
                ChannelId = ctx.Channel.Id,
                From = ctx.User.Id.ToString(),
                To = ctx.User.Id.ToString(),
                GuildId = ctx.Guild.Id,
                MessageId = ctx.Message.Id,
                Reason = $"Transfer from {fromAcc} to {toAcc}",
                TransactionDate = DateTime.UtcNow
            });

            if (!success)
            {
                await ctx.Channel.SendErrorAsync($"You do not have enough {_roki.Properties.CurrencyIcon} to transfer.").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"You've successfully transferred `{amount:N0}` {_roki.Properties.CurrencyIcon} from `{fromAcc}` to `{toAcc}`")).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Give(long amount, IGuildUser user, [Leftover] string message = null)
        {
            if (amount <= 0 || ctx.User.Id == user.Id || user.IsBot)
                return;

            if (string.IsNullOrWhiteSpace(message))
                message = "No Message";
            message = $"Gift from {ctx.User.Username} to {user.Username} - {message}";
            var success = await _currency.TransferAsync(ctx.User, user, message, amount, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id)
                .ConfigureAwait(false);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync($"You do not have enough {_roki.Properties.CurrencyNamePlural} to give.").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"{ctx.User.Username} gifted `{amount.FormatNumber()}` {_roki.Properties.CurrencyNamePlural} to {user.Mention}")).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [Priority(1)]
        public async Task CurrencyTransactions(int page = 1) =>
            await InternalCurrencyTransaction(ctx.User.Id, page);
        
        [RokiCommand, Description, Usage, Aliases]
        [OwnerOnly]
        [Priority(0)]
        public async Task CurrencyTransactions(IUser user, int page = 1) =>
            await InternalCurrencyTransaction(user.Id, page);

        private async Task InternalCurrencyTransaction(ulong userId, int page)
        {
            if (--page < 0)
                return;

            List<CurrencyTransaction> trans;
            using (var uow = _db.GetDbContext())
            {
                trans = uow.Transaction.GetTransactions(userId, page);
            }
            
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle($"{((SocketGuild) ctx.Guild)?.GetUser(userId)?.Username ?? userId.ToString()}'s Transactions History");

            var desc = "";
            foreach (var tran in trans)
            {
                var type = tran.Amount > 0 ? "🔵" : "🔴";
                var amount = tran.Amount.FormatNumber();
                if (tran.Reason.StartsWith("Gift from") && tran.From == userId.ToString())
                {
                    type = "🔴";
                    amount = amount.Insert(0, "-");
                }
                var date = Format.Code($"{tran.TransactionDate.ToLocalTime():HH:mm yyyy-MM-dd}");
                desc += $"{type} {tran.Reason?.Trim()} {date}\n\t\t{Format.Code(amount)} {_roki.Properties.CurrencyIcon}\n";
            }

            embed.WithDescription(desc)
                .WithFooter($"Page {page + 1}");
            
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
        
        public enum Account
        {
            Cash = 0,
            Stone = 0,
            Debit = 0,
            Investing = 1,
            Invest = 1,
            Inv = 1,
            Trading = 1
        }
    }
}