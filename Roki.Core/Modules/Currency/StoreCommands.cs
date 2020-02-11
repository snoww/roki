using System;
using System.Linq;
using System.Text;
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
            private readonly IMongoService _mongo;
            private readonly DiscordSocketClient _client;
            private readonly ICurrencyService _currency;

            private StoreCommands(DiscordSocketClient client, ICurrencyService currency, IMongoService mongo)
            {
                _client = client;
                _currency = currency;
                _mongo = mongo;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Store([Leftover] string itemName = null)
            {
                var cat = await _mongo.Context.GetStoreCatalogueAsync(Context.Guild.Id).ConfigureAwait(false);

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
                                var desc = $"{Format.Bold(c.Name)} | {type} | {(c.Quantity > 0 ? $"**{c.Quantity}** Remaining" : "**Sold Out**")} | `{c.Cost:N0}` {Roki.Properties.CurrencyIcon}";
                                return $"`{c.Id}:` {desc}\n\t{c.Description.TrimTo(120)}";
                            }));
                        return new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Stone Shop")
                            .WithDescription(catalogStr)
                            .WithFooter($"Page {page + 1}/{Math.Ceiling((double) cat.Count / itemsPerPage)} Use .buy to purchase items");
                    }
                                                
                    await Context.SendPaginatedMessageAsync(0, Catalog, cat.Count, 9).ConfigureAwait(false);
                    return;
                }

                var item = cat.FirstOrDefault(i => i.Name == itemName);
                if (item == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find the specified item.").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle($"`{item.Id}:` | {item.Name} | `{item.Cost:N0}` {Roki.Properties.CurrencyIcon}")
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
                var listing = await _mongo.Context.GetStoreItemByNameAsync(Context.Guild.Id, name).ConfigureAwait(false);
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

                await _mongo.Context.UpdateStoreItemAsync(Context.Guild.Id, name, amount);
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

                        if (await _mongo.Context.AddOrUpdateUserSubscriptionAsync(buyer.Id, Context.Guild.Id, listing.Id, listing.SubscriptionDays ?? 7)) 
                            break;
                        await ((IGuildUser) buyer).AddRoleAsync(await Service.GetRoleAsync(Context, listing.Details).ConfigureAwait(false));
                        break;
                    case Category.Power:
                        Enum.TryParse<Power>(listing.Details, out _);
                        await _mongo.Context.AddOrUpdateUserInventoryAsync(buyer.Id, Context.Guild.Id, listing.Id, amount).ConfigureAwait(false);
                        break;
                    case Category.Boost:
                        await _mongo.Context.AddOrUpdateUserSubscriptionAsync(buyer.Id, Context.Guild.Id, listing.Id, listing.SubscriptionDays ?? 7);
                        break;
                    case Category.Digital:
                    case Category.Virtual:
                    case Category.Irl:
                        break;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"{buyer.Mention} Purchase Successful!\nYou bought `{amount}x` {listing.Name}\n`{cost}` {Roki.Properties.CurrencyIcon} has been billed to your account"))
                    .ConfigureAwait(false);
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
                var guildId = Context.Guild.Id;
                var subs = (await _mongo.Context.GetUserAsync(Context.User.Id).ConfigureAwait(false)).Subscriptions
                    .Where(s => s.GuildId == guildId)
                    .ToList();
                
                var embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle($"{Context.User.Username}'s Active Subscriptions");
                if (subs.Count == 0)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("No active subscriptions.")).ConfigureAwait(false);
                    return;
                }
                var desc = new StringBuilder();
                var now = DateTime.UtcNow;
                foreach (var sub in subs)
                {
                    var listing = await _mongo.Context.GetStoreItemByIdAsync(guildId, sub.Id);
                    desc.AppendLine($"{listing.Name} - Expires in `{(sub.StartDate.AddDays(sub.Length) - now).ToReadableString()}`");
                }

                await Context.Channel.EmbedAsync(embed.WithDescription(desc.ToString())).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Inventory()
            {
                var guildId = Context.Guild.Id;
                var inv = (await _mongo.Context.GetUserAsync(Context.User.Id).ConfigureAwait(false)).Inventory
                    .Where(x => x.GuildId == guildId)
                    .ToList();
                
                var embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle($"{Context.User.Username}'s Inventory");
                string desc;
                if (inv.Count == 0)
                    desc = "Your inventory is empty";
                else
                    desc = string.Join("\n", inv
                        .Select(async i => $"{(await _mongo.Context.GetStoreItemByIdAsync(guildId, i.Id)).Name.ToTitleCase()}: {i.Quantity}"));

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