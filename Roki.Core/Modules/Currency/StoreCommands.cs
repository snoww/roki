using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Currency.Services;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Currency
{
    public partial class Currency
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class StoreCommands : RokiSubmodule<StoreService>
        {
            private readonly DiscordSocketClient _client;
            private readonly RokiContext _context;
            private readonly ICurrencyService _currency;
            private readonly IConfigurationService _config;

            private StoreCommands(DiscordSocketClient client, RokiContext context, ICurrencyService currency, IConfigurationService config)
            {
                _client = client;
                _context = context;
                _currency = currency;
                _config = config;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Store()
            {
                List<StoreItem> cat = await _context.Items.AsNoTracking()
                    .Where(x => x.GuildId == Context.Guild.Id && x.Quantity != null)
                    // soft limit of 100
                    .Take(100)
                    .ToListAsync();
                
                if (cat.Count == 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle($"{Context.Guild.Name}'s Shop")
                        .WithDescription("The store is currently empty. Please check back again later."));
                    return;
                }
                
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

                const int itemsPerPage = 9;

                EmbedBuilder Catalog(int page)
                {
                    int startAt = itemsPerPage * page;
                    string catalogStr = string.Join("\n", cat
                        .Skip(startAt)
                        .Take(itemsPerPage)
                        .Select(c =>
                        {
                            string type = c.Category == "Subscription" ? $"**{c.Duration}** Day {c.Category}" : c.Category;
                            string desc = $"{Format.Bold(c.Name)} | {type} | {(c.Quantity > 0 ? $"**{c.Quantity}** Remain" : "**Sold Out**")} | `{c.Price:N0}` {guildConfig.CurrencyIcon}";
                            return $"`{c.Id}` {desc}\n\t{c.Description.TrimTo(120)}";
                        }));
                    return new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Stone Shop")
                        .WithDescription(catalogStr)
                        .WithFooter($"Page {page + 1}/{Math.Ceiling((double) cat.Count / itemsPerPage)} Use {guildConfig.Prefix}buy <id> <quantity> to purchase items");
                }

                await Context.SendPaginatedMessageAsync(0, Catalog, cat.Count, 9).ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Store(string itemName)
            {
                StoreItem listing = await _context.Items.AsNoTracking()
                    .Where(x => x.GuildId == Context.Guild.Id && x.Name.ToLower() == itemName.ToLower() && x.Quantity != null)
                    .FirstOrDefaultAsync();
                
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

                if (listing == null)
                {
                    await Context.Channel.SendErrorAsync($"Cannot find the specified item.\nUse {guildConfig.Prefix}store to check out store items").ConfigureAwait(false);
                    return;
                }

                // todo handle null quantities
                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle($"`{listing.Id}` | {listing.Name} | `{listing.Price:N0}` {guildConfig.CurrencyIcon}")
                    .WithDescription(listing.Description)
                    .AddField("Category", $"{listing.Category}", true)
                    .AddField("Quantity", $"{(listing.Quantity > 0 ? $"**{listing.Quantity}** Remaining" : "**Sold Out**")}")
                    .AddField("Type", listing.Category == "Subscription" ? $"**{listing.Duration}** Day {listing.Duration}" : $"{listing.Category}", true);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

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
                
                StoreItem listing = await _context.Items.AsQueryable()
                    .Where(x => x.GuildId == Context.Guild.Id && x.Name.ToLower() == name.ToLower())
                    .FirstOrDefaultAsync();
                
                if (listing == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find this item. Use the ID of the item preferably.").ConfigureAwait(false);
                    return;
                }

                if (listing.Quantity == null)
                {
                    await Context.Channel.SendErrorAsync("Sorry. This item is not for sale, please check again later.").ConfigureAwait(false);
                    return;
                }

                if (listing.Quantity <= 0)
                {
                    await Context.Channel.SendErrorAsync("Sorry. This item is out of stock, please check again later.").ConfigureAwait(false);
                    return;
                }

                await InternalBuy(listing, amount).ConfigureAwait(false);
            }

            private async Task InternalBuy(StoreItem listing, int amount)
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

                if (listing.Quantity < amount || amount == 0)
                {
                    amount = listing.Quantity!.Value;
                }

                IUser buyer = Context.User;
                var seller = _client.GetUser(listing.SellerId) as IUser;
                long price = listing.Price * amount ?? 0;
                bool removed = await _currency.TransferCurrencyAsync(buyer.Id, seller.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id,
                    $"Store Purchase - #{listing.Id} x{amount}", price).ConfigureAwait(false);
                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"You do not have enough {guildConfig.CurrencyIcon} to purchase this item.").ConfigureAwait(false);
                    return;
                }

                listing.Quantity -= 1;
                var category = Enum.Parse<Category>(listing.Category);
                switch (category)
                {
                    case Category.Role:
                        if (listing.Duration == 0)
                        {
                            await ((IGuildUser) buyer).AddRoleAsync(await GetRoleAsync(listing.Details).ConfigureAwait(false));
                            break;
                        }

                        await _context.Subscriptions.AddAsync(new Subscription
                        {
                            UserId = buyer.Id,
                            GuildId = Context.Guild.Id,
                            ItemId = listing.Id,
                            Expiry = DateTime.UtcNow.AddDays(listing.Duration + 1)
                        });
                        
                        await ((IGuildUser) buyer).AddRoleAsync(await GetRoleAsync(listing.Details).ConfigureAwait(false));
                        break;
                    // todo
                    case Category.Power:
                    case Category.Boost:
                    case Category.Digital:
                    case Category.Virtual:
                    case Category.Irl:
                        break;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"{buyer.Mention} Purchase Successful!\nYou bought `{amount}x` {listing.Name}\n`{price}` {guildConfig.CurrencyIcon} has been billed to your account"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Subscriptions(int page = 1)
            {
                if (page <= 0)
                {
                    page = 1;
                }

                page -= 1;
                
                var subs = await _context.Subscriptions.Include(x => x.Item).AsNoTracking()
                    .Where(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id)
                    .Select(x => new
                    {
                        x.Item.Name,
                        x.Expiry
                    })
                    .Skip(page * 15)
                    .Take(15)
                    .ToListAsync();

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle($"{Context.User.Username}'s Active Subscriptions").WithFooter($"Page {page + 1}");
                if (subs.Count == 0)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription($"No active subscriptions.\nUse {_config.GetGuildPrefix(Context.Guild.Id)}store to buy some.")).ConfigureAwait(false);
                    return;
                }

                var desc = new StringBuilder();
                DateTime now = DateTime.UtcNow.Date;
                foreach (var sub in subs)
                {
                    desc.AppendLine($"{sub.Name} - Expires in `{(sub.Expiry - now).ToReadableString()}`");
                }

                await Context.Channel.EmbedAsync(embed.WithDescription(desc.ToString())).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Inventory(int page = 1)
            {
                if (page <= 0)
                {
                    page = 1;
                }

                page -= 1;
                var inv = await _context.Inventory.Include(x => x.Item).AsNoTracking()
                    .Where(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id)
                    .Select(x => new
                    {
                        x.Item.Name,
                        x.Quantity
                    })
                    .Skip(page * 15)
                    .Take(15)
                    .ToListAsync();

                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle($"{Context.User.Username}'s Inventory");
                if (inv.Count == 0)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription($"Your inventory is empty\nUse `{await _config.GetGuildPrefix(Context.Guild.Id)}store` to buy some items.")).ConfigureAwait(false);
                    return;
                }

                var desc = new StringBuilder();
                foreach (var item in inv)
                {
                    desc.AppendLine($"{item.Name}: {item.Quantity}");
                }

                embed.WithDescription(desc.ToString()).WithFooter($"Page {page + 1}");

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