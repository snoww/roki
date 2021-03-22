using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roki.Common;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Currency.Common;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Currency
{
    [RequireContext(ContextType.Guild)]
    public partial class Currency : RokiTopLevelModule
    {
        private readonly RokiContext _context;
        private readonly ICurrencyService _currency;
        private readonly IConfigurationService _config;

        public Currency(RokiContext context, ICurrencyService currency, IConfigurationService config)
        {
            _context = context;
            _currency = currency;
            _config = config;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Cash(IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle($"{user.Username}'s Cash Account")
                    .WithDescription($"`{await _currency.GetCurrencyAsync(user.Id, Context.Guild.Id):N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}")
                    .WithFooter($"{await _config.GetGuildPrefix(Context.Guild.Id)}$$ for Investing Account"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Investing(IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle($"{user.Username}'s Investing Account")
                    .WithDescription($"`{await _currency.GetInvestingAsync(user.Id, Context.Guild.Id):N2}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}")
                    .WithFooter($"{await _config.GetGuildPrefix(Context.Guild.Id)}$ for Cash Account"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Leaderboard(IUser user = null)
        {
            user ??= Context.User;

            var leaderboard = await _context.UserData.AsNoTracking().Include(x => x.User)
                .Where(x => x.GuildId == Context.Guild.Id && x.UserId != Roki.BotId)
                .OrderByDescending(x => x.Currency)
                .Take(9)
                .Select(x => new {x.User.Username, x.User.Discriminator, x.Currency})
                .ToListAsync();

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("Currency Leaderboard")
                .WithFooter($"To show other pages: {await _config.GetGuildPrefix(Context.Guild.Id)}lb <page>");
            if (leaderboard.Any(x => x.Username == user.Username && x.Discriminator == user.Discriminator))
            {
                int i = 1;
                foreach (var u in leaderboard)
                {
                    if (u.Username == user.Username && u.Discriminator == user.Discriminator)
                    {
                        embed.AddField($"#{i++} __{u.Username}#{u.Discriminator}__", $"`{u.Currency:N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}");
                    }
                    else
                    {
                        embed.AddField($"#{i++} {u.Username}#{u.Discriminator}", $"`{u.Currency:N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}");
                    }
                }
            }
            else
            {
                (long rank, string username, string discriminator, long currency) = await GetUserCurrencyLeaderboard(user.Id, Context.Guild.Id);
                int i = 1;
                foreach (var u in leaderboard.Take(8))
                {
                    embed.AddField($"#{i++} {u.Username}#{u.Discriminator}", $"`{u.Currency:N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}");
                }
                embed.AddField($"#{rank} __{username}#{discriminator}__", $"`{currency:N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}");
            }
            
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Leaderboard(int page)
        {
            if (page <= 0)
            {
                page = 1;
            }

            page -= 1;

            var leaderboard = await _context.UserData.AsNoTracking().Include(x => x.User)
                .Where(x => x.GuildId == Context.Guild.Id && x.UserId != Roki.BotId)
                .OrderByDescending(x => x.Currency)
                .Skip(page * 9)
                .Take(9)
                .Select(x => new {x.User.Username, x.User.Discriminator, x.Currency})
                .ToListAsync();

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("Currency Leaderboard")
                .WithFooter($"Page {page+1} | To show other pages: {await _config.GetGuildPrefix(Context.Guild.Id)}lb <page>");
            int i = 9 * page + 1;
            foreach (var user in leaderboard)
            {
                embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"`{user.Currency:N0}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}");
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases, RokiOptions(typeof(TransferOptions))]
        public async Task Transfer(long amount, params string[] location)
        {
            var args = new List<string> {amount.ToString()};
            args.AddRange(location);
            TransferOptions options = OptionsParser.ParseFrom(new TransferOptions(), args.ToArray());
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
            if (options == null)
            {
                // idk how to throw error to go to command error
                await Context.Channel.SendErrorAsync($"```\nError: Invalid arguments provided.```\n`{guildConfig.Prefix}h transfer` to check correct usage.").ConfigureAwait(false);
                return;
            }

            if (options.Amount <= 0)
            {
                await Context.Channel.SendErrorAsync($"You must transfer at least `1` {guildConfig.CurrencyIcon}").ConfigureAwait(false);
                return;
            }

            if (options.Direction == Direction.To && options.Account == Account.Investing || options.Direction == Direction.From && options.Account == Account.Cash)
            {
                if (!await _currency.TransferAccountAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, options.Amount, Account.Cash))
                {
                    await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyIcon} in your `{Account.Cash}` account to transfer.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"{Context.User.Mention} You've successfully transferred `{options.Amount:N0}` {guildConfig.CurrencyIcon}\nFrom `{Account.Cash}` to `{Account.Investing}`"))
                    .ConfigureAwait(false);
            }
            else
            {
                if (!await _currency.TransferAccountAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, options.Amount, Account.Investing))
                {
                    await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyIcon} in your `{Account.Investing}` account to transfer.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"{Context.User.Mention} You've successfully transferred `{options.Amount:N0}` {guildConfig.CurrencyIcon}\nFrom `{Account.Investing}` to `{Account.Cash}`"))
                    .ConfigureAwait(false);
            }
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

            var giveMsg = $"Gift from {Context.User.Username} to {user.Username} - {message}";

            bool success = await _currency.TransferCurrencyAsync(Context.User.Id, user.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, giveMsg, amount);
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
        [RequireUserPermission(GuildPermission.ManageRoles)]
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

            var transactions = await _context.Transactions.AsNoTracking()
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
            desc.AppendLine("```diff");
            // todo better formatting
            foreach (var trans in transactions)
            {
                if (trans.Sender == user.Id)
                {
                    desc.AppendLine(trans.Amount >= 0 ? $"-{trans.Amount,-8:N0}{$"#{trans.Id}",18}".Replace(' ', '.') : $"+{Math.Abs(trans.Amount),-8:N0}{$"#{trans.Id}",18}".Replace(' ', '.'));
                }
                else
                {
                    desc.AppendLine(trans.Amount >= 0 ? $"+{trans.Amount,-8:N0}{$"#{trans.Id}",18}".Replace(' ', '.') : $"-{Math.Abs(trans.Amount),-8:N0}{$"#{trans.Id}",18}".Replace(' ', '.'));
                }
            }

            desc.Append("```");

            embed.WithDescription(desc.ToString()).WithFooter($"Page {page + 1}");

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private async Task<(long, string, string, long)> GetUserCurrencyLeaderboard(ulong userId, ulong guildId)
        {
            var conn = (NpgsqlConnection) _context.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                select r.rank, r.username, r.discriminator, r.currency
                from (select u.username, u.discriminator, ud.uid, ud.currency, rank() over (order by ud.currency desc) rank
                      from user_data ud
                               inner join users as u on ud.uid = u.id
                      where ud.guild_id = (@guild_id)) r
                where r.uid = (@uid);", conn);
            cmd.Parameters.AddWithValue("guild_id", (long) guildId);
            cmd.Parameters.AddWithValue("uid", (long) userId);
            await cmd.PrepareAsync();
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            // rank | username | discriminator | currency
            (long, string, string, long) stats = (reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3));
            await conn.CloseAsync();
            return stats;
        }
    }

    public enum Account
    {
        Cash,
        Investing
    }

    public enum Direction
    {
        To,
        From
    }
}