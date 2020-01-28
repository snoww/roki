using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Rsvp.Services;

namespace Roki.Modules.Rsvp
{
    public class Rsvp : RokiTopLevelModule<RsvpService>
    {
        [RokiCommand, Description, Aliases, Usage]
        [RequireContext(ContextType.Guild)]
        public async Task Events([Leftover] string args = null)
        {
            var err = string.Format("`{0}events new/create`: Create a new event\n" +
                                    "`{0}events edit`: Edits an event\n" +
                                    "`{0}events list/ls <optional_page>`: Lists events in this server\n", Roki.Properties.Prefix);
            if (string.IsNullOrWhiteSpace(args))
            {
                await ctx.Channel.SendErrorAsync(err).ConfigureAwait(false);
                return;
            }

            if (args.Equals("new", StringComparison.OrdinalIgnoreCase) || args.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                await _service.CreateEvent(ctx).ConfigureAwait(false);
            }
            else if (args.Equals("edit", StringComparison.OrdinalIgnoreCase) || args.Equals("e", StringComparison.OrdinalIgnoreCase))
            {
                await _service.EditEvent(ctx).ConfigureAwait(false);
            }
            else if (args.StartsWith("list", StringComparison.OrdinalIgnoreCase) || args.Equals("ls", StringComparison.OrdinalIgnoreCase))
            {
                var split = args.Split();
                var page = 0;
                if (split.Length > 1)
                {
                    if (!int.TryParse(split[1], out page))
                    {
                        await ctx.Channel.SendErrorAsync("Could not parse page number. Please try again.").ConfigureAwait(false);
                        return;
                    }
                }
                var events = _service.ListEvents(ctx.Guild.Id, page);
                var embed = new EmbedBuilder().WithOkColor().WithTitle("List of Events")
                    .WithDescription($"{string.Join("\n", events.Select(e => $"`#{e.Id}` **{e.Name}** in `{(e.StartDate - DateTimeOffset.UtcNow).ToReadableString()}` https://discordapp.com/channels/{e.GuildId}/{e.ChannelId}/{e.MessageId}"))}");

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendErrorAsync(err).ConfigureAwait(false);
            }
        }
    }
}