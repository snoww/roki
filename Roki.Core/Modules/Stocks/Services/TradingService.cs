using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NLog;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Stocks.Services
{
    public class TradingService : IRokiService
    {
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        private readonly RokiContext _context;
        private readonly ICurrencyService _currency;
        private readonly IConfigurationService _service;
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _factory;

        public TradingService(IHttpClientFactory factory, IRokiConfig config, RokiContext context, ICurrencyService currency, IConfigurationService service)
        {
            _factory = factory;
            _config = config;
            _context = context;
            _currency = currency;
            _service = service;
        }

        public async Task<decimal> GetInvestingAccount(ulong userId, ulong guildId) => await _currency.GetInvestingAsync(userId, guildId);

        public async Task<decimal?> GetLatestPriceAsync(string symbol)
        {
            using HttpClient http = _factory.CreateClient();
            try
            {
                string result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
                return decimal.Parse(result);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public async Task<Investment> GetUserInvestment(ulong userId, ulong guildId, string symbol)
        {
            return await _context.Investments.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId && x.Symbol == symbol);
        }

        public async Task SellShares(ulong userId, ulong guildId, string symbol, long shares, decimal price)
        {
            // position guaranteed to already exist
            Investment position = await _context.Investments.AsQueryable().SingleOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId && x.Symbol == symbol) ?? new Investment(userId, guildId, symbol, -shares);
            position.Shares -= shares;
            position.Trades.Add(new Trade(-shares, price));
            await _currency.AddInvestingAsync(userId, guildId, price * shares);
            await _context.SaveChangesAsync();
        }

        // public async Task ShortShares(ulong userId, ulong guildId, string symbol, long shares, decimal price)
        // {
        //     Investment position = await _context.Investments.AsQueryable().SingleOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId && x.Symbol == symbol) ?? new Investment(userId, guildId, symbol, -shares);
        //     position.Shares -= shares;
        //     position.Trades.Add(new Trade(-shares, price));
        //     await _currency.AddInvestingAsync(userId, guildId, price * shares);
        //     await _context.SaveChangesAsync();
        // }

        public async Task BuyShares(ulong userId, ulong guildId, string symbol, long shares, decimal price)
        {
            await _currency.RemoveInvestingAsync(userId, guildId, price * shares);
            Investment position = await _context.Investments.AsQueryable().SingleOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId && x.Symbol == symbol);
            if (position == null)
            {
                position = new Investment(userId, guildId, symbol, shares);
                await _context.Investments.AddAsync(position);
            }
            else
            {
                position.Shares += shares;
            }
            
            position.Trades.Add(new Trade(shares, price));

            await _context.SaveChangesAsync();
        }
    }
    
    public enum Status
    {
        NotEnoughInvesting = -1,
        NotEnoughShares = -2,
        TooMuchLeverage = -3,
        OwnsShortShares = -4,
        OwnsLongShares = -5,
        Success = 1
    }
}