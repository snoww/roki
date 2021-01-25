using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;
using StackExchange.Redis;

namespace Roki.Modules.Currency.Services
{
    public class PickDropService : IRokiService
    {
        private readonly IDatabase _cache;
        private readonly IMongoService _mongo;
        private readonly SemaphoreSlim _pickLock = new SemaphoreSlim(1, 1);
        private readonly Random _rng = new Random();


        public PickDropService(CommandHandler command, IRedisCache cache, IMongoService mongo)
        {
            _mongo = mongo;
            _cache = cache.Redis.GetDatabase();
            command.OnMessageNoTrigger += CurrencyGeneration;
        }

        private ConcurrentDictionary<ulong, DateTime> LastGenerations { get; } = new ConcurrentDictionary<ulong, DateTime>();

        private async Task CurrencyGeneration(IUserMessage message)
        {
            if (!(message is SocketUserMessage) || !(message.Channel is ITextChannel channel) || Roki.Properties.CurrencyGenIgnoredChannels.Contains(channel.Id))
            {
                return;
            }
            
            // impossible to drop if value is set to 0.01
            if (Roki.Properties.CurrencyGenerationChance <= 0.01)
            {
                return;
            }

            DateTime lastGeneration = LastGenerations.GetOrAdd(channel.Id, DateTime.MinValue);

            if (DateTime.UtcNow - TimeSpan.FromMinutes(Roki.Properties.CurrencyGenerationCooldown) < lastGeneration)
            {
                return;
            }

            double num = _rng.Next(0, 100) + Roki.Properties.CurrencyGenerationChance * 100;
            if (num > 100 && LastGenerations.TryUpdate(channel.Id, DateTime.UtcNow, lastGeneration))
            {
                int drop = Roki.Properties.CurrencyDropAmount;
                int? dropMax = Roki.Properties.CurrencyDropAmountMax;

                if (dropMax > drop)
                {
                    drop = _rng.Next(drop, dropMax.Value + 1);
                }

                if (_rng.Next(0, 101) == 100)
                {
                    drop = Roki.Properties.CurrencyDropAmountRare ?? 100;
                }

                if (drop > 0)
                {
                    await _cache.StringIncrementAsync($"gen:{channel.GuildId}:{channel.Id}", drop).ConfigureAwait(false);
                    await _cache.StringAppendAsync($"gen:log:{channel.GuildId}:{channel.Id}", $"{message.Id},").ConfigureAwait(false);

                    string prefix = Roki.Properties.Prefix;
                    string toSend = drop == 1
                        ? $"{Roki.Properties.CurrencyIcon} A random {Roki.Properties.CurrencyName} appeared! Type `{prefix}pick` to pick it up."
                        : $"{Roki.Properties.CurrencyIcon} {drop} random {Roki.Properties.CurrencyNamePlural} appeared! Type `{prefix}pick` to pick them up.";
                    // TODO add images to send with drop
                    await channel.SendMessageAsync(toSend).ConfigureAwait(false);
                }
            }
        }

        public async Task<long> PickAsync(ITextChannel channel, IUser user)
        {
            await _pickLock.WaitAsync();
            try
            {
                RedisValue rawAmount = await _cache.StringGetAsync($"gen:{channel.GuildId}:{channel.Id}").ConfigureAwait(false);
                RedisValue messages = await _cache.StringGetAsync($"gen:log:{channel.GuildId}:{channel.Id}").ConfigureAwait(false);
                
                if (rawAmount.IsNullOrEmpty || messages.IsNullOrEmpty)
                {
                    return 0;
                }
                
                var amount = (long) rawAmount;

                await _cache.StringIncrementAsync($"currency:{channel.Guild.Id}:{user.Id}", amount, CommandFlags.FireAndForget)
                    .ConfigureAwait(false);
                await _mongo.Context.UpdateUserCurrencyAsync(user.Id, amount).ConfigureAwait(false);

                try
                {
                    List<ulong> ids = (from id in ((string) messages).Split(',')
                            where !string.IsNullOrWhiteSpace(id)
                            select ulong.Parse(id))
                        .ToList();
                    await channel.DeleteMessagesAsync(ids).ConfigureAwait(false);
                }
                catch
                {
                    //
                }
                finally
                {
                    _cache.KeyDelete($"gen:{channel.GuildId}:{channel.Id}");
                    _cache.KeyDelete($"gen:log:{channel.GuildId}:{channel.Id}");
                }

                return amount;
            }
            finally
            {
                _pickLock.Release();
            }
        }

        public async Task<bool> DropAsync(ICommandContext ctx, IUser user, long amount)
        {
            User dbUser = await _mongo.Context.GetOrAddUserAsync(user).ConfigureAwait(false);
            if (dbUser.Currency < amount)
            {
                return false;
            }

            await _cache.StringIncrementAsync($"gen:{ctx.Guild.Id}:{ctx.Channel.Id}", amount).ConfigureAwait(false);
            await _cache.StringIncrementAsync($"currency:{ctx.Guild.Id}:{user.Id}", -amount, CommandFlags.FireAndForget)
                .ConfigureAwait(false);

            await _mongo.Context.UpdateUserCurrencyAsync(dbUser, -amount).ConfigureAwait(false);

            IUserMessage msg = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithDescription($"{user.Username} dropped {amount:N0} {Roki.Properties.CurrencyIcon}")
                    .WithFooter($"Use {Roki.Properties.Prefix}pick to pick it up"))
                .ConfigureAwait(false);

            await _cache.StringAppendAsync($"gen:log:{ctx.Guild.Id}:{ctx.Channel.Id}", $"{msg.Id},").ConfigureAwait(false);

            return true;
        }
    }
}