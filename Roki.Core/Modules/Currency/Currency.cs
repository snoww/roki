using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

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

        private readonly IRokiDbService _dbService;
        private readonly IConfigurationService _config;

        public Currency(IRokiDbService dbService, IConfigurationService config)
        {
            _dbService = dbService;
            _config = config;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Cash(IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{user.Mention}'s Cash Account:\n`{await _dbService.GetUserCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}")
                    .WithFooter(".$$ for Investing Account"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Investing(IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{user.Mention}'s Investing Account:\n`{await _dbService.GetUserInvestingAsync(Context.User.Id, Context.Guild.Id):N2}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}")
                    .WithFooter(".$ for Cash Account"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Leaderboard(int page = 1)
        {
            if (page <= 0)
            {
                page = 1;
            }

            page -= 1;

            var leaderboard = await _dbService.Context.UserData.AsNoTracking().Include(x => x.User).AsAsyncEnumerable()
                .Where(x => x.GuildId == Context.Guild.Id)
                .OrderByDescending(x => x.Currency)
                .Skip(page * 9)
                .Take(9)
                .Select(x => new { x.User.Username, x.User.Discriminator, x.Currency})
                .ToListAsync();
            
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("Currency Leaderboard");
            int i = 9 * page + 1;
            foreach (var user in leaderboard)
            {
                embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"`{user.Currency:N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}");
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Transfer(Account account, long amount)
        {
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
            if (amount <= 0)
            {
                await Context.Channel.SendErrorAsync($"You must transfer at least `1` {guildConfig.CurrencyIcon}").ConfigureAwait(false);
                return;
            }

            var success = false;
            var fromAcc = "";
            var toAcc = "";
            
            UserData data = await _dbService.Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id);
            if ((int) account == 0)
            {
                if (data.Investing >= amount)
                {
                    data.Investing -= amount;
                    data.Currency += amount;
                    success = true;
                    fromAcc = "Investing Account";
                    toAcc = "Cash Account";
                }
            }
            else if ((int) account == 1)
            {
                if (data.Currency >= amount)
                {
                    data.Currency -= amount;
                    data.Investing += amount;
                    success = true;
                    fromAcc = "Cash Account";
                    toAcc = "Investing Account";
                }
            }

            if (!success)
            {
                await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyIcon} in your `{fromAcc}` to transfer.").ConfigureAwait(false);
                return;
            }
            
            await _dbService.Context.Transactions.AddAsync(new Transaction
            {
                GuildId = Context.Guild.Id,
                Sender = Context.User.Id,
                Recipient = Context.User.Id,
                Amount = amount,
                ChannelId = Context.Channel.Id,
                MessageId = Context.Message.Id,
                Description = $"Transfer from {fromAcc} to {toAcc}"
            });
            
            await _dbService.SaveChangesAsync();

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"{Context.User.Mention} You've successfully transferred `{amount:N0}` {guildConfig.CurrencyIcon}\nFrom `{fromAcc}` to `{toAcc}`")).ConfigureAwait(false);
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
            bool success = await _dbService.TransferCurrencyAsync(Context.User.Id, user.Id,Context.Guild.Id, Context.Channel.Id, Context.Message.Id, message, amount)
                .ConfigureAwait(false);
            
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

            if (!success)
            {
                await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyNamePlural} to give.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"{Context.User.Username} gifted `{amount:N0}` {guildConfig.CurrencyNamePlural} to {user.Mention}\\")
                .AddField("Message", message)).ConfigureAwait(false);
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
            if (page <= 0)
            {
                page = 1;
            }

            page -= 1;

            var transactions = await _dbService.Context.Transactions.AsNoTracking().AsAsyncEnumerable()
                .Where(x => x.GuildId == Context.Guild.Id && (x.Sender == user.Id || x.Recipient == user.Id))
                .OrderByDescending(x => x.Id)
                .Select(x => new {x.Id, x.Sender, x.Recipient, x.Amount})
                .Skip(page * 15)
                .Take(15)
                .ToListAsync();

            if (transactions.Count == 0)
            {
                await Context.Channel.SendErrorAsync("No transactions").ConfigureAwait(false);
                return;
            }

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle($"{user.Username}'s Transactions History");
            
            var desc = new StringBuilder();
            // todo better formatting
            foreach (var trans in transactions)
            {
                if (trans.Sender == user.Id)
                {
                    desc.Append("+ ").Append(trans.Amount.ToString("N0")).Append(" #").Append(trans.Id).AppendLine();
                }
                else
                {
                    desc.Append("- ").Append(trans.Amount.ToString("N0")).Append(" #").Append(trans.Id).AppendLine();
                }
            }

            embed.WithDescription(desc.ToString()).WithFooter($"Page {page + 1}");

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}