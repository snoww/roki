using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Core.Services.Impl;
using Roki.Extentions;

namespace Roki.Modules.Utility
{
    public partial class Utiltiy : RokiTopLevelModule
    {
        private readonly DiscordSocketClient _client;
        private readonly IStatsService _stats;
        private readonly IConfiguration _config;
        private readonly Roki _roki;

        public Utiltiy(Roki roki, DiscordSocketClient client, IStatsService stats, IConfiguration config)
        {
            _client = client;
            _stats = stats;
            _config = config;
            _roki = roki;
        }

        [RokiCommand, Usage, Description, Aliases]
        public async Task Stats()
        {
            var ownerId = string.Join("\n", _config.OwnerIds);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                ownerId = "-";
            }

            await ctx.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName($"Roki v{StatsService.BotVersion}")
                        .WithIconUrl("https://i.imgur.com/KmPRRKh.png"))
                    .AddField(efb => efb.WithName("Author").WithValue(_stats.Author).WithIsInline(true))
                    .AddField(efb => efb.WithName("Bot ID").WithValue(_client.CurrentUser.Id.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Commands ran").WithValue(_stats.CommandsRan.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("Messages").WithValue(_stats.MessageCounter).WithIsInline(true))
                    .AddField(efb => efb.WithName("Memory").WithValue($"{_stats.Heap} MB").WithIsInline(true))
                    .AddField(efb => efb.WithName("Owner ID").WithValue(ownerId).WithIsInline(true))
                    .AddField(efb => efb.WithName("Uptime").WithValue(_stats.GetUptimeString("\n")).WithIsInline(true)));
        }
    }
}