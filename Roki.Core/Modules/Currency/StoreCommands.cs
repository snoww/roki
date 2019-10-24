using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Currency.Services;
using Roki.Services;

namespace Roki.Modules.Currency
{
    public partial class Currency
    {
        [Group]
        public class StoreCommands : RokiSubmodule<StoreService>
        {
            private readonly DiscordSocketClient _client;
            private readonly ICurrencyService _currency;
            private const string Stone = "<:stone:269130892100763649>";

            private StoreCommands(DiscordSocketClient client, ICurrencyService currency)
            {
                _client = client;
                _currency = currency;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Store()
            {
                var cat = _service.GetStoreCatalog();

                const int itemsPerPage = 10;
                EmbedBuilder Catalog(int page)
                {
                    var startAt = itemsPerPage * page;
                    var catalogStr = string.Join("\n", cat
                        .Skip(startAt)
                        .Take(itemsPerPage)
                        .Select(c =>
                        {
                            var desc = $"{Format.Bold(c.ItemName)} | Sold By: {_client.GetUser(c.SellerId).Username} | {(c.Quantity > 0 ? $"{c.Quantity} Remaining" : "Sold Out")} | {c.Cost} {Stone}";
                            return $"`ID: {c.Id}.` {desc}\n\t{c.Description.TrimTo(120)}";
                        }));
                    return new EmbedBuilder().WithOkColor()
                        .WithTitle("Stone Shop")
                        .WithDescription(catalogStr)
                        .WithFooter($"Page {page + 1}/{Math.Ceiling((double) cat.Count / itemsPerPage)}");
                }

                await ctx.SendPaginatedConfirmAsync(0, Catalog, cat.Count, 9).ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Sell()
            {
            
            }
        
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(0)]
            public async Task Buy(string name)
            {
                var listing = _service.GetListingByName(name);
                if (listing == null)
                {
                    await ctx.Channel.SendErrorAsync("Cannot find this item. Make sure the name matches exactly, or use ID").ConfigureAwait(false);
                    return;
                }

                if (listing.Quantity <= 0)
                {
                    await ctx.Channel.SendErrorAsync("Sorry. This item is out of stock, please come back later.").ConfigureAwait(false);
                    return;
                }

                var buyer = ctx.User;
                var seller = _client.GetUser(listing.SellerId);
                var removed = await _currency.TransferAsync(buyer, seller, $"Store Purchase - ID {listing.Id}", listing.Cost, 
                    ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id).ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync("You do not have enough to purchase this item.").ConfigureAwait(false);
                    return;
                }

                Enum.TryParse<ListingCategory>(listing.Category, out var category); 
                Enum.TryParse<ListingType>(listing.Type, out var type); 
                switch (category)
                {
                    case ListingCategory.Role:
                        var guildUser = buyer as IGuildUser;
                        if (type == ListingType.OneTime)
                        {
                        }
                        break;
                    case ListingCategory.Digital:
                    case ListingCategory.Virtual:
                        break;
                    case ListingCategory.Irl:
                        break;
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(1)]
            public async Task Buy(int id)
            {
                var listing = _service.GetListingById(id);
                if (listing == null)
                {
                    await ctx.Channel.SendErrorAsync("Cannot find this item with this ID.").ConfigureAwait(false);
                    return;
                }

                await Buy(listing.ItemName).ConfigureAwait(false);
            }
        
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Modify()
            {
            
            }

            private enum ListingCategory
            {
                Role,
                Digital,
                Virtual,
                Irl,
            }
            
            private enum ListingType
            {
                Subscription,
                OneTime
            }
        }
    }
}