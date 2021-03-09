using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using StackExchange.Redis;

namespace Roki.Modules.Currency.Services
{
    public class PickDropService : IRokiService
    {
        private readonly IDatabase _cache;
        private readonly IRokiDbService _dbService;
        private readonly IConfigurationService _config;
        private readonly SemaphoreSlim _pickLock = new(1, 1);
        private readonly Random _rng = new();


        public PickDropService(CommandHandler command, IRedisCache cache, IRokiDbService dbService, IConfigurationService config)
        {
            _dbService = dbService;
            _config = config;
            _cache = cache.Redis.GetDatabase();
            command.OnMessageNoTrigger += CurrencyGeneration;
        }

        private ConcurrentDictionary<ulong, DateTime> LastGenerations { get; } = new();

        private async Task CurrencyGeneration(IUserMessage message)
        {
            if (message is not SocketUserMessage || message.Channel is not ITextChannel channel)
            {
                return;
            }

            if (!await _config.CurrencyGenEnabled(channel.Id))
            {
                return;
            }

            GuildConfig guildConfig = await _config.GetGuildConfigAsync(channel.GuildId);

            // impossible to drop if value is set below 0.01
            if (guildConfig.CurrencyGenerationChance < 0.01)
            {
                return;
            }

            DateTime lastGeneration = LastGenerations.GetOrAdd(channel.Id, DateTime.MinValue);

            DateTime now = DateTime.UtcNow;

            if (now - TimeSpan.FromSeconds(guildConfig.CurrencyGenerationCooldown) < lastGeneration)
            {
                return;
            }

            if (guildConfig.CurrencyGenerationChance * 100 >= _rng.Next(0, 101) && LastGenerations.TryUpdate(channel.Id, now, lastGeneration))
            {
                int drop = guildConfig.CurrencyDropAmount;
                int? dropMax = guildConfig.CurrencyDropAmountMax;

                if (dropMax > drop)
                {
                    drop = _rng.Next(drop, dropMax.Value + 1);
                }

                if (_rng.Next(0, 101) == 100)
                {
                    drop = guildConfig.CurrencyDropAmountRare;
                }

                if (drop > 0)
                {
                    await _cache.StringIncrementAsync($"gen:{channel.Id}", drop).ConfigureAwait(false);

                    string toSend = drop == 1
                        ? $"{guildConfig.CurrencyIcon} A random {guildConfig.CurrencyName} appeared! Type `{guildConfig.Prefix}pick` to pick it up."
                        : $"{guildConfig.CurrencyIcon} {drop} random {guildConfig.CurrencyNamePlural} appeared! Type `{guildConfig.Prefix}pick` to pick them up.";
                    // TODO add images to send with drop
                    IUserMessage sent = await channel.SendMessageAsync(toSend).ConfigureAwait(false);
                    await _cache.StringAppendAsync($"gen:log:{channel.Id}", $"{sent.Id},").ConfigureAwait(false);
                }
            }
        }

        public async Task<long> PickAsync(ITextChannel channel, ulong userId, ulong messageId)
        {
            await _pickLock.WaitAsync();
            try
            {
                RedisValue rawAmount = await _cache.StringGetAsync($"gen:{channel.Id}").ConfigureAwait(false);
                RedisValue messages = await _cache.StringGetAsync($"gen:log:{channel.Id}").ConfigureAwait(false);

                if (rawAmount.IsNullOrEmpty || messages.IsNullOrEmpty)
                {
                    return 0;
                }

                var amount = (long) rawAmount;

                UserData data = await _dbService.Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == userId && x.GuildId == channel.Guild.Id);
                data.Currency += amount;
                
                await _dbService.Context.Transactions.AddAsync(new Transaction
                {
                    GuildId = channel.Guild.Id,
                    Sender = userId,
                    Recipient = Roki.BotId,
                    Amount = amount,
                    ChannelId = channel.Id,
                    MessageId = messageId,
                    Description = "Picked currency"
                });

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
                    await _dbService.SaveChangesAsync();
                    _cache.KeyDelete($"gen:{channel.Id}");
                    _cache.KeyDelete($"gen:{channel.Id}");
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
            UserData data = await _dbService.Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == ctx.User.Id && x.GuildId == ctx.Guild.Id);
            if (data.Currency < amount)
            {
                return false;
            }

            await _cache.StringIncrementAsync($"gen:{ctx.Channel.Id}", amount).ConfigureAwait(false);
            data.Currency -= amount;

            GuildConfig guildConfig = await _config.GetGuildConfigAsync(ctx.Guild.Id);

            IUserMessage msg = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithDescription($"{ctx.User.Mention} dropped {amount:N0} {guildConfig.CurrencyIcon}")
                    .WithFooter($"Use {guildConfig.Prefix}pick to pick it up"))
                .ConfigureAwait(false);
            
            await _dbService.Context.Transactions.AddAsync(new Transaction
            {
                GuildId = ctx.Guild.Id,
                Sender = ctx.User.Id,
                Recipient = Roki.BotId,
                Amount = amount,
                ChannelId = ctx.Channel.Id,
                MessageId = ctx.Message.Id,
                Description = "Dropped currency"
            });

            await _cache.StringAppendAsync($"gen:log:{ctx.Channel.Id}", $"{msg.Id},").ConfigureAwait(false);
            await _dbService.SaveChangesAsync();
            return true;
        }
    }
}