using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Utility.Services;

namespace Roki.Modules.Utility
{
    public partial class Utility : RokiTopLevelModule<RainbowRoleService>
    {
        private readonly DiscordSocketClient _client;
        private readonly IRokiConfig _config;
        private readonly Roki _roki;
        private readonly IStatsService _stats;

        public Utility(Roki roki, DiscordSocketClient client, IStatsService stats, IRokiConfig config)
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
            if (string.IsNullOrWhiteSpace(ownerId)) ownerId = "-";

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

        [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
        public async Task Pins()
        {
            var pins = await ctx.Channel.GetPinnedMessagesAsync().ConfigureAwait(false);
            if (pins.Count < 1)
            {
                await ctx.Channel.SendErrorAsync("No pins in this channel");
                return;
            }
            var pin = pins.First();
// TODO handle pinned embeds?
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(pin.Author.Username)
                .WithDescription(pin.Content)
                .WithFooter($"{pin.Timestamp.ToLocalTime():hh:mm tt MM/dd/yyyy}");
            
            await ctx.Channel.EmbedAsync(embed);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Say([Leftover] string message = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            await ctx.Channel.SendMessageAsync(message).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SayRaw([Leftover] string message = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            
            await ctx.Channel.SendMessageAsync(ctx.Message.Content).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task UserDetails(IUser user)
        {
            await ctx.Channel.SendMessageAsync(
                $"UserId: {user.Id}\nUsername: {user.Username}\nDiscriminator: {user.Discriminator}\nAvatarId: {user.AvatarId}\n").ConfigureAwait(false);
        }
    }
}