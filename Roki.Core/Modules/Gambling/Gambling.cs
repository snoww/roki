using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Gambling
{
    [RequireContext(ContextType.Guild)]
    public partial class Gambling : RokiTopLevelModule
    {
        private readonly IRokiDbService _dbService;
        private readonly IConfigurationService _config;
        private readonly Random _rng = new();

        public Gambling(IRokiDbService dbService, IConfigurationService config)
        {
            _dbService = dbService;
            _config = config;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task BetRoll(long amount)
        {
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

            if (amount < guildConfig.BetRollMin)
            {
                return;
            }
            
            UserData userData = await _dbService.Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id);
            UserData botData = await _dbService.Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == Roki.BotId && x.GuildId == Context.Guild.Id);

            bool removed = await _dbService.RemoveCurrencyAsync(userData, botData, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetRoll Entry", amount).ConfigureAwait(false);
            if (!removed)
            {
                await Context.Channel.SendErrorAsync($"Not enough {guildConfig.CurrencyIcon}\n" +
                                                     $"You have `{userData.Currency:N0}`")
                    .ConfigureAwait(false);
                return;
            }

            int roll = _rng.Next(1, 101);
            var rollStr = $"{Context.User.Mention} rolled `{roll}`.";
            if (roll < 70)
            {
                await Context.Channel.SendErrorAsync($"{rollStr}\nBetter luck next time.\n" +
                                                     $"New Balance: `{userData.Currency:N0}` {guildConfig.CurrencyIcon}")
                    .ConfigureAwait(false);
            }
            else
            {
                double payout;
                if (roll < 91)
                {
                    payout = amount * guildConfig.BetRoll71Multiplier;
                }
                else if (roll < 100)
                {
                    payout = amount * guildConfig.BetRoll92Multiplier;
                }
                else
                {
                    payout = amount * guildConfig.BetRoll100Multiplier;
                }

                await _dbService.AddCurrencyAsync(userData, botData, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetRoll Payout", (long) Math.Ceiling(payout)).ConfigureAwait(false);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"{rollStr}\nCongratulations, you won `{payout:N0}` {guildConfig.CurrencyIcon}\n" +
                                         $"New Balance: `{userData.Currency:N0}` {guildConfig.CurrencyIcon}"))
                    .ConfigureAwait(false);
            }

            await _dbService.SaveChangesAsync();
        }
    }
}