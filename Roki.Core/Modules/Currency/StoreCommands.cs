using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services;
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
            private readonly ICurrencyService _currency;
            private readonly DbService _db;
            private const string Stone = "<:stone:269130892100763649>";

            private StoreCommands(ICurrencyService currency, DbService db)
            {
                _currency = currency;
                _db = db;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Store()
            {
                List<Store> cat;
                using (var uow = _db.GetDbContext())
                {
                    cat = uow.Store.GetStoreCatalog();
                }

                const int itemsPerPage = 10;
                EmbedBuilder Catalog(int page)
                {
                    var startAt = itemsPerPage * page;
                    var number = startAt + 1;
                    var catalogStr = string.Join("\n", cat
                        .Skip(startAt)
                        .Take(itemsPerPage)
                        .Select(c =>
                        {
                            var desc = $"{Format.Bold(c.ItemName)} - {(c.Quantity > 0 ? $"{c.Quantity} Remaining" : "Sold Out")} - {c.Cost} {Stone}";
                            return $"`{number++}.` {desc}\n\t{c.Description.TrimTo(100)}";
                        }));
                    return new EmbedBuilder().WithOkColor()
                        .WithTitle("Stone Shop")
                        .WithDescription(catalogStr)
                        .WithFooter($"Page {page + 1}/{Math.Ceiling((double) cat.Count / itemsPerPage)}");
                }

                await ctx.SendPaginatedConfirmAsync(0, Catalog, cat.Count, 9).ConfigureAwait(false);
            }
        }
    }
}