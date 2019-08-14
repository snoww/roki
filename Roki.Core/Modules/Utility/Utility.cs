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

        [RokiCommand, Description, Usage, Aliases]
        public async Task Stats()
        {
            var ownerId = string.Join("\n", _config.OwnerIds);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                ownerId = "-";
            }

            await ctx.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName($"Roki v{StatsService.BotVersion}").WithIconUrl("https://i.imgur.com/KmPRRKh.png"))
                    .AddField("Author", _stats.Author, true)
                    .AddField("Bot ID", _client.CurrentUser.Id.ToString(), true)
                    .AddField("Commands ran", _stats.CommandsRan.ToString(), true)
                    .AddField("Messages", _stats.MessageCounter, true)
                    .AddField("Memory", $"{_stats.Heap} MB", true)
                    .AddField("Owner ID", ownerId, true)
                    .AddField("Uptime", _stats.GetUptimeString("\n"), true)
                    .AddField("Presence", $"{_stats.TextChannels} Text Channels\n{_stats.VoiceChannels} Voice Channels", true));
        }
    }
}