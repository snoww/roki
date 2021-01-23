using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Utility.Services;

namespace Roki.Modules.Utility
{
    public partial class Utility : RokiTopLevelModule<UtilityService>
    {
        [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
        public async Task Pins()
        {
            IReadOnlyCollection<IMessage> pins = await Context.Channel.GetPinnedMessagesAsync().ConfigureAwait(false);
            if (pins.Count < 1)
            {
                await Context.Channel.SendErrorAsync("No pins in this channel");
                return;
            }

            IMessage pin = pins.First();
// TODO handle pinned embeds?
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle(pin.Author.Username)
                .WithDescription(pin.Content)
                .WithFooter($"{pin.Timestamp.ToLocalTime():hh:mm tt MM/dd/yyyy}");

            await Context.Channel.EmbedAsync(embed);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Uwu([Leftover] string message = null)
        {
            if (message == null)
            {
                IEnumerable<IMessage> msgs = await Context.Channel.GetMessagesAsync(Context.Message, Direction.Before, 5).FlattenAsync();
                IMessage? userMsg = msgs.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Content));
                if (userMsg == null)
                {
                    await Context.Channel.SendErrorAsync("nyothing to uwufy uwu").ConfigureAwait(false);
                    return;
                }

                message = userMsg.Content;
            }

            string uwuize = Service.Uwulate(message);
            await Context.Channel.SendMessageAsync(uwuize).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Ping()
        {
            var sw = Stopwatch.StartNew();
            IUserMessage msg = await Context.Channel.SendMessageAsync("🏓").ConfigureAwait(false);
            sw.Stop();
            await msg.DeleteAsync().ConfigureAwait(false);

            var embed = new EmbedBuilder();
            embed.WithDynamicColor(Context)
                .WithAuthor("Pong! 🏓")
                .WithDescription($"Currently {(int) sw.Elapsed.TotalMilliseconds} ms");

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Say([Leftover] string message = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await Context.Channel.SendMessageAsync(message).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SayRaw([Leftover] string message = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await Context.Channel.SendMessageAsync(Format.Code(message)).ConfigureAwait(false);
        }
    }
}