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

            private StoreCommands(DiscordSocketClient client, ICurrencyService currency)
            {
                _client = client;
                _currency = currency;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Store([Leftover] string itemName = null)
            {
                var cat = Service.GetStoreCatalog();

                if (itemName == null)
                {
                    const int itemsPerPage = 9;
                    EmbedBuilder Catalog(int page)
                    {
                        var startAt = itemsPerPage * page;
                        var catalogStr = string.Join("\n", cat
                            .Skip(startAt)
                            .Take(itemsPerPage)
                            .Select(c =>
                            {
                                var type = c.Type == "Subscription" ? $"**{c.SubscriptionDays}** Day {c.Type}" : c.Type;
                                var desc = $"{Format.Bold(c.Item)} | {type} | {(c.Quantity > 0 ? $"**{c.Quantity}** Remaining" : "**Sold Out**")} | `{c.Cost:N0}` {Roki.Properties.CurrencyIcon}";
                                return $"`{c.Id}:` {desc}\n\t{c.Description.TrimTo(120)}";
                            }));
                        return new EmbedBuilder().WithOkColor()
                            .WithTitle("Stone Shop")
                            .WithDescription(catalogStr)
                            .WithFooter($"Page {page + 1}/{Math.Ceiling((double) cat.Count / itemsPerPage)} Use .buy to purchase items");
                    }
                                                
                    await Context.SendPaginatedConfirmAsync(0, Catalog, cat.Count, 9).ConfigureAwait(false);
                    return;
                }

                var item = int.TryParse(itemName, out var id) ? cat.FirstOrDefault(i => i.Id == id) : cat.FirstOrDefault(i => i.Item == itemName);
                if (item == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find the specified item.").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle($"`{item.Id}:` | {item.Item} | `{item.Cost:N0}` {Roki.Properties.CurrencyIcon}")
                    .WithDescription(item.Description)
                    .AddField("Category", $"{item.Category}", true)
                    .AddField("Quantity", $"{(item.Quantity > 0 ? $"**{item.Quantity}** Remaining" : "**Sold Out**")}")
                    .AddField("Type", item.Type == "Subscription" ? $"**{item.SubscriptionDays}** Day {item.Type}" : $"{item.Type}", true);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
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
                var listing = Service.GetListingByName(name);
                if (listing == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find this item. Make sure the name matches exactly, or use ID").ConfigureAwait(false);
                    return;
                }

                if (listing.Quantity <= 0)
                {
                    await Context.Channel.SendErrorAsync("Sorry. This item is out of stock, please come back later.").ConfigureAwait(false);
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
                        await Context.Channel.SendErrorAsync("You need to buy at least `1`.").ConfigureAwait(false);
                        return;
                    }
                }

                if (listing.Quantity < amount)
                    amount = listing.Quantity;

                var buyer = Context.User;
                var seller = _client.GetUser(listing.SellerId) as IUser;
                var cost = listing.Cost * amount;
                var removed = await _currency.TransferAsync(buyer.Id, seller.Id, $"Store Purchase - ID {listing.Id} x{amount}", cost, 
                    Context.Guild.Id, Context.Channel.Id, Context.Message.Id).ConfigureAwait(false);
                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"You do not have enough {Roki.Properties.CurrencyIcon} to purchase this item.").ConfigureAwait(false);
                    return;
                }

                await Service.UpdateListingAsync(listing.Id, amount).ConfigureAwait(false);
                Enum.TryParse<Category>(listing.Category, out var category); 
                Enum.TryParse<Type>(listing.Type, out var type); 
                switch (category)
                {
                    case Category.Role:
                        if (type == Type.OneTime)
                        {
                            await ((IGuildUser) buyer).AddRoleAsync(await Service.GetRoleAsync(Context, listing.Details).ConfigureAwait(false));
                            break;
                        }

                        if (await Service.GetOrUpdateSubAsync(buyer.Id, listing.Id, listing.SubscriptionDays * amount ?? 7)) break;
                        await ((IGuildUser) buyer).AddRoleAsync(await Service.GetRoleAsync(Context, listing.Details).ConfigureAwait(false));
                        await Service.AddNewSubscriptionAsync(buyer.Id, Context.Guild.Id, listing.Id, listing.Category.ToUpperInvariant(),listing.Details, DateTime.UtcNow, 
                                DateTime.UtcNow + new TimeSpan(listing.SubscriptionDays * amount ?? 7, 0, 0, 0)
                            ).ConfigureAwait(false);
                        break;
                    case Category.Power:
                        Enum.TryParse<Power>(listing.Details, out _);
                        await Service.UpdateInventoryAsync(buyer.Id, listing.Details, amount).ConfigureAwait(false);
                        break;
                    case Category.Boost:
                        if (await Service.GetOrUpdateSubAsync(buyer.Id, listing.Id, listing.SubscriptionDays * amount ?? 7)) break;
                        await Service.AddNewSubscriptionAsync(buyer.Id, Context.Guild.Id, listing.Id, listing.Category.ToUpperInvariant(), listing.Details, DateTime.UtcNow, 
                            DateTime.UtcNow + new TimeSpan(listing.SubscriptionDays * amount ?? 7, 0, 0, 0)
                        ).ConfigureAwait(false);
                        break;
                    case Category.Digital:
                    case Category.Virtual:
                    case Category.Irl:
                        break;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"{buyer.Mention} Purchase Successful!\nYou bought `{amount}x` {listing.Item}\n`{cost}` {Roki.Properties.CurrencyIcon} has been billed to your account"))
                    .ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(1)]
            public async Task Buy(int id, string quantity = "1")
            {
                var listing = Service.GetListingById(id);
                if (listing == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find this item with this ID.").ConfigureAwait(false);
                    return;
                }

                await Buy(listing.Item, quantity).ConfigureAwait(false);
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
                var subs = Service.GetUserSubscriptions(Context.User.Id);
                var embed = new EmbedBuilder().WithOkColor().WithTitle($"{Context.User.Username}'s Active Subscriptions");
                if (subs.Count == 0)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("No active subscriptions.")).ConfigureAwait(false);
                    return;
                }
                var desc = "";
                var now = DateTime.UtcNow;
                foreach (var sub in subs)
                {
                    var listing = Service.GetListingById(sub.ItemId);
                    desc += $"{listing.Item} - Expires in `{(sub.EndDate - now).ToReadableString()}`\n";
                }

                await Context.Channel.EmbedAsync(embed.WithDescription(desc)).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Inventory()
            {
                var inv = await Service.GetOrCreateInventoryAsync(Context.User.Id).ConfigureAwait(false);
                var embed = new EmbedBuilder().WithOkColor().WithTitle($"{Context.User.Username}'s Inventory");
                string desc;
                if (inv.Count == 0)
                    desc = "Your inventory is empty";
                else
                    desc = string.Join("\n", inv
                        .Select(i => $"{i.Name.ToTitleCase()}: {i.Quantity}"));

                embed.WithDescription(desc);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
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