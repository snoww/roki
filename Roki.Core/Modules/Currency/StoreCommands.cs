using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MongoDB.Bson;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Currency.Services;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Currency
{
    public partial class Currency
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class StoreCommands : RokiSubmodule<StoreService>
        {
            private readonly DiscordSocketClient _client;
            private readonly ICurrencyService _currency;
            private readonly IMongoService _mongo;

            private StoreCommands(DiscordSocketClient client, ICurrencyService currency, IMongoService mongo)
            {
                _client = client;
                _currency = currency;
                _mongo = mongo;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Store([Leftover] string itemName = null)
            {
                Dictionary<string, Listing> cat = await _mongo.Context.GetStoreCatalogueAsync(Context.Guild.Id).ConfigureAwait(false);
                if (cat.Count == 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Stone Shop")
                        .WithDescription("The store is currently empty. Please check back again later."));
                    return;
                }
                
                GuildConfig guildConfig = await _mongo.Context.GetGuildConfigAsync(Context.Guild.Id);

                if (itemName == null)
                {
                    const int itemsPerPage = 9;

                    EmbedBuilder Catalog(int page)
                    {
                        int startAt = itemsPerPage * page;
                        string catalogStr = string.Join("\n", cat
                            .Skip(startAt)
                            .Take(itemsPerPage)
                            .Select(c =>
                            {
                                string type = c.Value.Type == "Subscription" ? $"**{c.Value.SubscriptionDays}** Day {c.Value.Type}" : c.Value.Type;
                                var desc = $"{Format.Bold(c.Value.Name)} | {type} | {(c.Value.Quantity > 0 ? $"**{c.Value.Quantity}** Remain" : "**Sold Out**")} | `{c.Value.Cost:N0}` {guildConfig.CurrencyIcon}";
                                return $"`{c.Key}` {desc}\n\t{c.Value.Description.TrimTo(120)}";
                            }));
                        return new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Stone Shop")
                            .WithDescription(catalogStr)
                            .WithFooter($"Page {page + 1}/{Math.Ceiling((double) cat.Count / itemsPerPage)} Use {guildConfig.Prefix}buy <id> <quantity> to purchase items");
                    }

                    await Context.SendPaginatedMessageAsync(0, Catalog, cat.Count, 9).ConfigureAwait(false);
                    return;
                }

                (string listingId, Listing listing) = cat.FirstOrDefault(i => i.Value.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
                if (listing == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find the specified item.").ConfigureAwait(false);
                    return;
                }

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle($"`{listingId}` | {listing.Name} | `{listing.Cost:N0}` {guildConfig.CurrencyIcon}")
                    .WithDescription(listing.Description)
                    .AddField("Category", $"{listing.Category}", true)
                    .AddField("Quantity", $"{(listing.Quantity > 0 ? $"**{listing.Quantity}** Remaining" : "**Sold Out**")}")
                    .AddField("Type", listing.Type == "Subscription" ? $"**{listing.SubscriptionDays}** Day {listing.Type}" : $"{listing.Type}", true);

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
            [Priority(0)]
            public async Task Buy(string quantity, [Leftover] string name)
            {
                int amount;
                if (quantity.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    amount = 0;
                }
                else
                {
                    if (!int.TryParse(quantity, out amount))
                    {
                        amount = 1;
                    }

                    if (amount <= 0)
                    {
                        amount = 1;
                    }
                }

                (string, Listing) listing;
                if (Regex.IsMatch(name, "^[a-fA-F0-9]{6}"))
                {
                    listing = await _mongo.Context.GetStoreItemByIdAsync(Context.Guild.Id, name)
                        .ConfigureAwait(false);
                }
                else
                {
                    listing = await _mongo.Context.GetStoreItemByNameAsync(Context.Guild.Id, name).ConfigureAwait(false);
                }

                if (listing.Item2 == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find this item. Use the ID of the item preferably.").ConfigureAwait(false);
                    return;
                }

                if (listing.Item2.Quantity <= 0)
                {
                    await Context.Channel.SendErrorAsync("Sorry. This item is out of stock, please check again later.").ConfigureAwait(false);
                    return;
                }

                await InternalBuy(listing.Item1, listing.Item2, amount).ConfigureAwait(false);
            }

            private async Task InternalBuy(string id, Listing listing, int amount)
            {
                GuildConfig guildConfig = await _mongo.Context.GetGuildConfigAsync(Context.Guild.Id);
                if (listing.Quantity < amount || amount == 0)
                {
                    amount = listing.Quantity;
                }

                IUser buyer = Context.User;
                var seller = _client.GetUser(listing.SellerId) as IUser;
                long cost = listing.Cost * amount;
                bool removed = await _currency.TransferAsync(buyer, seller, $"Store Purchase - ID {id} x{amount}", cost,
                    Context.Guild.Id, Context.Channel.Id, Context.Message.Id).ConfigureAwait(false);
                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyIcon} to purchase this item.").ConfigureAwait(false);
                    return;
                }

                await _mongo.Context.UpdateStoreItemAsync(Context.Guild.Id, id, -amount);
                var category = Enum.Parse<Category>(listing.Category);
                var type = Enum.Parse<Type>(listing.Type);
                switch (category)
                {
                    case Category.Role:
                        if (type == Type.OneTime)
                        {
                            await ((IGuildUser) buyer).AddRoleAsync(await GetRoleAsync(listing.Details).ConfigureAwait(false));
                            break;
                        }

                        if (await _mongo.Context.AddOrUpdateUserSubscriptionAsync(buyer, Context.Guild.Id.ToString(), id, listing.SubscriptionDays ?? 7))
                        {
                            break;
                        }

                        await ((IGuildUser) buyer).AddRoleAsync(await GetRoleAsync(listing.Details).ConfigureAwait(false));
                        break;
                    case Category.Power:
                        Enum.TryParse<Power>(listing.Details, out _);
                        await _mongo.Context.AddOrUpdateUserInventoryAsync(buyer, Context.Guild.Id.ToString(), id, amount).ConfigureAwait(false);
                        break;
                    case Category.Boost:
                        await _mongo.Context.AddOrUpdateUserSubscriptionAsync(buyer, Context.Guild.Id.ToString(), id, listing.SubscriptionDays ?? 7);
                        break;
                    case Category.Digital:
                    case Category.Virtual:
                    case Category.Irl:
                        break;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"{buyer.Mention} Purchase Successful!\nYou bought `{amount}x` {listing.Name}\n`{cost}` {guildConfig.CurrencyIcon} has been billed to your account"))
                    .ConfigureAwait(false);
            }

//            [RokiCommand, Description, Usage, Aliases]
//            [RequireContext(ContextType.Guild)]
//            public async Task Modify()
//            {
//            
//            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Subscriptions()
            {
                Dictionary<string, Subscription> subs = (await _mongo.Context.GetOrAddUserAsync(Context.User, Context.Guild.Id.ToString()).ConfigureAwait(false)).Data[Context.Guild.Id.ToString()].Subscriptions;

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle($"{Context.User.Username}'s Active Subscriptions");
                if (subs.Count == 0)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("No active subscriptions.")).ConfigureAwait(false);
                    return;
                }

                var desc = new StringBuilder();
                DateTime now = DateTime.UtcNow.Date;
                foreach ((string id, Subscription sub) in subs)
                {
                    (_, Listing listing) = await _mongo.Context.GetStoreItemByObjectIdAsync(Context.Guild.Id, id);
                    desc.AppendLine($"{listing.Name} - Expires in `{(sub.EndDate - now).ToReadableString()}`");
                }

                await Context.Channel.EmbedAsync(embed.WithDescription(desc.ToString())).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Inventory()
            {
                Dictionary<string, Item> inv = (await _mongo.Context.GetOrAddUserAsync(Context.User, Context.Guild.Id.ToString()).ConfigureAwait(false)).Data[Context.Guild.Id.ToString()].Inventory;

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle($"{Context.User.Username}'s Inventory");
                string desc;
                if (inv.Count == 0)
                {
                    desc = "Your inventory is empty";
                }
                else
                {
                    desc = string.Join("\n", inv
                        .Select(i => $"{_mongo.Context.GetStoreItemByObjectIdAsync(Context.Guild.Id, i.Key).Result.Item2.Name.ToTitleCase()}: {i.Value.Quantity}"));
                }

                embed.WithDescription(desc);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            private async Task<IRole> GetRoleAsync(string roleName)
            {
                if (!roleName.Contains("rainbow", StringComparison.OrdinalIgnoreCase))
                {
                    return Context.Guild.Roles.First(r => r.Name == roleName);
                }

                List<IRole> rRoles = Context.Guild.Roles.Where(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase)).ToList();
                IRole first = rRoles.First();
                IReadOnlyCollection<IGuildUser> users = await Context.Guild.GetUsersAsync().ConfigureAwait(false);

                foreach (IGuildUser user in users)
                {
                    IRole rRole = user.GetRoles().FirstOrDefault(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase));
                    if (rRole == null) continue;
                    rRoles.Remove(rRole);
                }

                return rRoles.First() ?? first;
            }

            private enum Category
            {
                Role,
                Boost,
                Power,
                Digital,
                Virtual,
                Irl
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
                SlowMode
            }
        }
    }
}