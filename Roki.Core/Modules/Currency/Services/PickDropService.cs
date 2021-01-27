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
        private readonly SemaphoreSlim _pickLock = new(1, 1);
        private readonly Random _rng = new();


        public PickDropService(CommandHandler command, IRedisCache cache, IMongoService mongo)
        {
            _mongo = mongo;
            _cache = cache.Redis.GetDatabase();
            command.OnMessageNoTrigger += CurrencyGeneration;
        }

        private ConcurrentDictionary<ulong, DateTime> LastGenerations { get; } = new();

        private async Task CurrencyGeneration(IUserMessage message)
        {
            if (!(message is SocketUserMessage) || !(message.Channel is ITextChannel channel))
            {
                return;
            }

            ChannelConfig channelConfig = await _mongo.Context.GetChannelConfigAsync(channel);
            if (!channelConfig.CurrencyGeneration)
            {
                return;
            }

            GuildConfig guildConfig = await _mongo.Context.GetGuildConfigAsync(channel.GuildId);

            // impossible to drop if value is set below 0.01
            if (guildConfig.CurrencyGenerationChance < 0.01)
            {
                return;
            }

            DateTime lastGeneration = LastGenerations.GetOrAdd(channel.Id, DateTime.MinValue);

            if (DateTime.UtcNow - TimeSpan.FromMinutes(guildConfig.CurrencyGenerationCooldown) < lastGeneration)
            {
                return;
            }

            if (guildConfig.CurrencyGenerationChance * 100 >= _rng.Next(0, 101) && LastGenerations.TryUpdate(channel.Id, DateTime.UtcNow, lastGeneration))
            {
                int drop = guildConfig.CurrencyDropAmount;
                int? dropMax = guildConfig.CurrencyDropAmountMax;

                if (dropMax > drop)
                {
                    drop = _rng.Next(drop, dropMax.Value + 1);
                }

                if (_rng.Next(0, 101) == 100)
                {
                    drop = guildConfig.CurrencyDropAmountRare ?? 100;
                }

                if (drop > 0)
                {
                    await _cache.StringIncrementAsync($"gen:{channel.GuildId}:{channel.Id}", drop).ConfigureAwait(false);

                    string toSend = drop == 1
                        ? $"{guildConfig.CurrencyIcon} A random {guildConfig.CurrencyName} appeared! Type `{guildConfig.Prefix}pick` to pick it up."
                        : $"{guildConfig.CurrencyIcon} {drop} random {guildConfig.CurrencyNamePlural} appeared! Type `{guildConfig.Prefix}pick` to pick them up.";
                    // TODO add images to send with drop
                    IUserMessage sent = await channel.SendMessageAsync(toSend).ConfigureAwait(false);
                    await _cache.StringAppendAsync($"gen:log:{channel.GuildId}:{channel.Id}", $"{sent.Id},").ConfigureAwait(false);
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

                RedisValue currency = await _cache.StringGetAsync($"currency:{channel.Guild.Id}:{user.Id}").ConfigureAwait(false);
                if (!currency.HasValue)
                {
                    long balance = await _mongo.Context.GetUserCurrency(user, channel.GuildId.ToString());
                    await _cache.StringSetAsync($"currency:{channel.Guild.Id}:{user.Id}", balance, TimeSpan.FromDays(7)).ConfigureAwait(false);
                }

                await _cache.StringIncrementAsync($"currency:{channel.Guild.Id}:{user.Id}", amount, CommandFlags.FireAndForget)
                    .ConfigureAwait(false);
                await _mongo.Context.UpdateUserCurrencyAsync(user, channel.GuildId.ToString(), amount).ConfigureAwait(false);

                try
                {
                    List<ulong> ids = ((string) messages).Split(',')
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(ulong.Parse)
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

        public async Task<bool> DropAsync(ICommandContext ctx, long amount)
        {
            var guildId = ctx.Guild.Id.ToString();
            User dbUser = await _mongo.Context.GetOrAddUserAsync(ctx.User, guildId).ConfigureAwait(false);
            if (dbUser.Data[guildId].Currency < amount)
            {
                return false;
            }

            await _cache.StringIncrementAsync($"gen:{guildId}:{ctx.Channel.Id}", amount).ConfigureAwait(false);
            await _cache.StringIncrementAsync($"currency:{guildId}:{ctx.User.Id}", -amount, CommandFlags.FireAndForget)
                .ConfigureAwait(false);

            await _mongo.Context.UpdateUserCurrencyAsync(dbUser, guildId, -amount).ConfigureAwait(false);
            
            GuildConfig guildConfig = await _mongo.Context.GetGuildConfigAsync(ctx.Guild.Id);
            IUserMessage msg = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithDescription($"{ctx.User.Username} dropped {amount:N0} {guildConfig.CurrencyIcon}")
                    .WithFooter($"Use {guildConfig.Prefix}pick to pick it up"))
                .ConfigureAwait(false);

            await _cache.StringAppendAsync($"gen:log:{guildId}:{ctx.Channel.Id}", $"{msg.Id},").ConfigureAwait(false);

            return true;
        }
    }
}