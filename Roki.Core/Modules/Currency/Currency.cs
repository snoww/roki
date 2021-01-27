using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Currency
{
    [RequireContext(ContextType.Guild)]
    public partial class Currency : RokiTopLevelModule
    {
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

        private readonly ICurrencyService _currency;
        private readonly IMongoService _mongo;
        private readonly IConfigurationService _config;

        public Currency(ICurrencyService currency, IMongoService mongo, IConfigurationService config)
        {
            _currency = currency;
            _mongo = mongo;
            _config = config;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Cash([Leftover] IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{user.Mention}'s Cash Account:\n`{await _currency.GetCurrency(user, Context.Guild.Id):N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}")
                    .WithFooter(".$$ for Investing Account"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Investing([Leftover] IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{user.Mention}'s Investing Account:\n`{_mongo.Context.GetUserInvesting(user, Context.Guild.Id.ToString()):N2}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}")
                    .WithFooter(".$ for Cash Account"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Leaderboard([Leftover] int page = 1)
        {
            if (page <= 0)
            {
                page = 1;
            }

            page -= 1;

            IEnumerable<User> list = await _mongo.Context.GetCurrencyLeaderboardAsync(Context.Guild.Id.ToString(), page).ConfigureAwait(false);
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("Currency Leaderboard");
            int i = 9 * page + 1;
            foreach (User user in list) embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"`{user.Data[Context.Guild.Id.ToString()].Currency:N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}");

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Transfer(Account account, long amount)
        {
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
            if (amount == 0)
            {
                await Context.Channel.SendErrorAsync($"You must transfer at least `1` {guildConfig.CurrencyIcon}").ConfigureAwait(false);
                return;
            }

            var success = false;
            var fromAcc = "";
            var toAcc = "";
            if ((int) account == 0)
            {
                success = await _mongo.Context.TransferCurrencyAsync(Context.User, Context.Guild.Id.ToString(), -amount).ConfigureAwait(false);
                if (success)
                {
                    await _currency.CacheChangeAsync(Context.User, Context.Guild.Id.ToString(), amount).ConfigureAwait(false);
                }

                fromAcc = "Investing Account";
                toAcc = "Cash Account";
            }
            else if ((int) account == 1)
            {
                success = await _mongo.Context.TransferCurrencyAsync(Context.User, Context.Guild.Id.ToString(), amount).ConfigureAwait(false);
                if (success)
                {
                    await _currency.CacheChangeAsync(Context.User, Context.Guild.Id.ToString(), -amount).ConfigureAwait(false);
                }

                fromAcc = "Cash Account";
                toAcc = "Investing Account";
            }

            if (!success)
            {
                await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyIcon} in your `{fromAcc}` to transfer.").ConfigureAwait(false);
                return;
            }

            await _mongo.Context.AddTransaction(new Transaction
            {
                Amount = amount,
                ChannelId = Context.Channel.Id,
                From = Context.User.Id,
                To = Context.User.Id,
                GuildId = Context.Guild.Id,
                MessageId = Context.Message.Id,
                Reason = $"Transfer from {fromAcc} to {toAcc}"
            });

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"{Context.User.Mention} You've successfully transferred `{amount:N0}` {guildConfig.CurrencyIcon}\nFrom `{fromAcc}` ➡️ `{toAcc}`")).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Give(long amount, IGuildUser user, [Leftover] string message = null)
        {
            if (amount <= 0 || Context.User.Id == user.Id || user.IsBot)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                message = "No Message";
            }

            message = $"Gift from {Context.User.Username} to {user.Username} - {message}";
            bool success = await _currency.TransferAsync(Context.User, user, message, amount, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                .ConfigureAwait(false);
            
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

            if (!success)
            {
                await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyNamePlural} to give.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"{Context.User.Username} gifted `{amount:N0}` {guildConfig.CurrencyNamePlural} to {user.Mention}")).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [Priority(1)]
        public async Task CurrencyTransactions(int page = 1)
        {
            await InternalCurrencyTransaction(Context.User, page);
        }

        [RokiCommand, Description, Usage, Aliases]
        [OwnerOnly]
        [Priority(0)]
        public async Task CurrencyTransactions(IUser user, int page = 1)
        {
            await InternalCurrencyTransaction(user, page);
        }

        private async Task InternalCurrencyTransaction(IUser user, int page)
        {
            if (--page < 0)
            {
                return;
            }

            IEnumerable<Transaction> trans = _mongo.Context.GetTransactions(user.Id, page);

            if (trans == null)
            {
                await Context.Channel.SendErrorAsync("No transactions").ConfigureAwait(false);
                return;
            }

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle($"{user.Username}'s Transactions History");
            
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

            var desc = new StringBuilder();
            foreach (Transaction tran in trans)
            {
                string type = tran.Amount > 0 ? "🔵" : "🔴";
                var amount = tran.Amount.ToString("N0");
                if (tran.Reason.StartsWith("Gift from") && tran.From == user.Id)
                {
                    type = "🔴";
                    amount = amount.Insert(0, "-");
                }

                // var date = Format.Code($"{tran.Date:HH:mm yyyy-MM-dd}");
                desc.AppendLine($"{type} - {tran.Reason?.Trim()} {Format.Code(amount)} {guildConfig.CurrencyIcon}");
            }

            embed.WithDescription(desc.ToString()).WithFooter($"Page {page + 1}");

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}