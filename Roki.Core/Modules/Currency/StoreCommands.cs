using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Core.Services.Database.Models;
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
            private readonly Roki _roki;

            private StoreCommands(DiscordSocketClient client, ICurrencyService currency, Roki roki)
            {
                _client = client;
                _currency = currency;
                _roki = roki;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Store([Leftover] string itemName = null)
            {
                var cat = _service.GetStoreCatalog();

                if (itemName == null)
                {
                    const int itemsPerPage = 10;
                    EmbedBuilder Catalog(int page)
                    {
                        var startAt = itemsPerPage * page;
                        var catalogStr = string.Join("\n", cat
                            .Skip(startAt)
                            .Take(itemsPerPage)
                            .Select(c =>
                            {
                                var type = c.Type == "Subscription" ? $"**{c.SubscriptionDays}** Day {c.Type}" : c.Type;
                                var desc = $"{Format.Bold(c.ItemName)} | {type} | {(c.Quantity > 0 ? $"**{c.Quantity}** Remaining" : "**Sold Out**")} | `{c.Cost:N0}` {_roki.Properties.CurrencyIcon}";
                                return $"`{c.Id}:` {desc}\n\t{c.Description.TrimTo(120)}";
                            }));
                        return new EmbedBuilder().WithOkColor()
                            .WithTitle("Stone Shop")
                            .WithDescription(catalogStr)
                            .WithFooter($"Page {page + 1}/{Math.Ceiling((double) cat.Count / itemsPerPage)} Use .buy to purchase items");
                    }
                                                
                    await ctx.SendPaginatedConfirmAsync(0, Catalog, cat.Count, 9).ConfigureAwait(false);
                    return;
                }

                var item = int.TryParse(itemName, out var id) ? cat.FirstOrDefault(i => i.Id == id) : cat.FirstOrDefault(i => i.ItemName == itemName);
                if (item == null)
                {
                    await ctx.Channel.SendErrorAsync("Cannot find the specified item.").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle($"`{item.Id}:` | {item.ItemName} | `{item.Cost:N0}` {_roki.Properties.CurrencyIcon}")
                    .WithDescription(item.Description)
                    .AddField("Category", $"{item.Category}", true)
                    .AddField("Quantity", $"{(item.Quantity > 0 ? $"**{item.Quantity}** Remaining" : "**Sold Out**")}")
                    .AddField("Type", item.Type == "Subscription" ? $"**{item.SubscriptionDays}** Day {item.Type}" : $"{item.Type}", true);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            
//            [RokiCommand, Description, Usage, Aliases]
//            [RequireContext(ContextType.Guild)]
//            [RequireUserPermission(GuildPermission.Administrator)]
//            public async Task Sell()
//            {
//                
//            }
        
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(0)]
            public async Task Buy(string name, string quantity = "1")
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

                int amount;
                if (quantity.Equals("all", StringComparison.OrdinalIgnoreCase))
                    amount = listing.Quantity;
                else
                {
                    int.TryParse(quantity, out amount);
                    if (amount <= 0)
                    {
                        await ctx.Channel.SendErrorAsync("You need to buy at least `1`.").ConfigureAwait(false);
                        return;
                    }
                }

                if (listing.Quantity < amount)
                    amount = listing.Quantity;

                var buyer = ctx.User;
                var seller = _client.GetUser(listing.SellerId) as IUser;
                var cost = listing.Cost * amount;
                var removed = await _currency.TransferAsync(buyer.Id, seller.Id, $"Store Purchase - ID {listing.Id} x{amount}", cost, 
                    ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id).ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"You do not have enough {_roki.Properties.CurrencyIcon} to purchase this item.").ConfigureAwait(false);
                    return;
                }

                await _service.UpdateListingAsync(listing.Id, amount).ConfigureAwait(false);
                Enum.TryParse<Category>(listing.Category, out var category); 
                Enum.TryParse<Type>(listing.Type, out var type); 
                switch (category)
                {
                    case Category.Role:
                        if (type == Type.OneTime)
                        {
                            await ((IGuildUser) buyer).AddRoleAsync(await _service.GetRoleAsync(ctx, listing.ItemDetails).ConfigureAwait(false));
                            break;
                        }

                        if (await _service.GetOrUpdateSubAsync(buyer.Id, listing.Id, listing.SubscriptionDays * amount ?? 7)) break;
                        await ((IGuildUser) buyer).AddRoleAsync(await _service.GetRoleAsync(ctx, listing.ItemDetails).ConfigureAwait(false));
                        await _service.AddNewSubscriptionAsync(buyer.Id, ctx.Guild.Id, listing.Id, listing.Category.ToUpperInvariant(),listing.ItemDetails, DateTime.UtcNow, 
                                DateTime.UtcNow + new TimeSpan(listing.SubscriptionDays * amount ?? 7, 0, 0, 0)
                            ).ConfigureAwait(false);
                        break;
                    case Category.Power:
                        Enum.TryParse<Power>(listing.ItemDetails, out _);
                        await _service.UpdateInventoryAsync(buyer.Id, listing.ItemDetails, amount).ConfigureAwait(false);
                        break;
                    case Category.Boost:
                        if (await _service.GetOrUpdateSubAsync(buyer.Id, listing.Id, listing.SubscriptionDays * amount ?? 7)) break;
                        await _service.AddNewSubscriptionAsync(buyer.Id, ctx.Guild.Id, listing.Id, listing.Category.ToUpperInvariant(), listing.ItemDetails, DateTime.UtcNow, 
                            DateTime.UtcNow + new TimeSpan(listing.SubscriptionDays * amount ?? 7, 0, 0, 0)
                        ).ConfigureAwait(false);
                        break;
                    case Category.Digital:
                    case Category.Virtual:
                    case Category.Irl:
                        break;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"{buyer.Mention} Purchase Successful!\nYou bought `{amount}x` {listing.ItemName}\n`{cost}` {_roki.Properties.CurrencyIcon} has been billed to your account"))
                    .ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(1)]
            public async Task Buy(int id, string quantity = "1")
            {
                var listing = _service.GetListingById(id);
                if (listing == null)
                {
                    await ctx.Channel.SendErrorAsync("Cannot find this item with this ID.").ConfigureAwait(false);
                    return;
                }

                await Buy(listing.ItemName).ConfigureAwait(false);
            }
        
//            [RokiCommand, Description, Usage, Aliases]
//            [RequireContext(ContextType.Guild)]
//            public async Task Modify()
//            {
//            
//            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Subscriptions()
            {
                var subs = _service.GetUserSubscriptions(ctx.User.Id);
                var embed = new EmbedBuilder().WithOkColor().WithTitle($"{ctx.User.Username}'s Active Subscriptions");
                if (subs.Count == 0)
                {
                    await ctx.Channel.EmbedAsync(embed.WithDescription("No active subscriptions.")).ConfigureAwait(false);
                    return;
                }
                var desc = "";
                foreach (var sub in subs)
                {
                    var listing = _service.GetListingById(sub.ItemId);
                    desc += $"{listing.ItemName} - Expires: {sub.StartDate - sub.EndDate:d\\.hh\\:mm}\n";
                }

                await ctx.Channel.EmbedAsync(embed.WithDescription(desc)).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Inventory()
            {
                var inv = await _service.GetOrCreateInventoryAsync(ctx.User.Id).ConfigureAwait(false);
                var embed = new EmbedBuilder().WithOkColor().WithTitle($"{ctx.User.Username}'s Inventory");
                string desc;
                if (inv.Count == 0)
                    desc = "Your inventory is empty";
                else
                    desc = string.Join("\n", inv
                        .Select(i => $"{i.Name.ToTitleCase()}: {i.Quantity}"));

                embed.WithDescription(desc);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            private enum Category
            {
                Role,
                Boost,
                Power,
                Digital,
                Virtual,
                Irl,
            }
            
            private enum Type
            {
                Subscription,
                OneTime
            }
            
            private enum Power
            {
                Timeout,
                Mute,
                DeleteMessage,
                SlowMode,
            }
        }
    }
}