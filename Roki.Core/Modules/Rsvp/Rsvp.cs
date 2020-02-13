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
                await Context.Channel.SendErrorAsync(err).ConfigureAwait(false);
                return;
            }

            if (args.Equals("new", StringComparison.OrdinalIgnoreCase) || args.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                await Service.CreateEvent(Context).ConfigureAwait(false);
            }
            else if (args.Equals("edit", StringComparison.OrdinalIgnoreCase) || args.Equals("e", StringComparison.OrdinalIgnoreCase))
            {
                await Service.EditEvent(Context).ConfigureAwait(false);
            }
            else if (args.StartsWith("list", StringComparison.OrdinalIgnoreCase) || args.Equals("ls", StringComparison.OrdinalIgnoreCase))
            {
                var events = await Service.GetActiveGuildEvents(Context.Guild.Id).ConfigureAwait(false);
                if (events.Count == 0)
                {
                    await Context.Channel.SendErrorAsync("No active events on this server.").ConfigureAwait(false);
                }
                
                var split = args.Split();
                var page = 0;
                if (split.Length > 1)
                {
                    if (!int.TryParse(split[1], out page))
                    {
                        await Context.Channel.SendErrorAsync("Could not parse page number. Please try again.").ConfigureAwait(false);
                        return;
                    }

                    if (page > events.Count)
                    {
                        page = 0;
                    }
                }

                var eventMessage = events
                    .Skip(page * 9)
                    .Take(5)
                    .Select(e =>
                        $"`#{e.Id.Pid}` **{e.Name}** in `{(e.StartDate - DateTimeOffset.UtcNow).ToReadableString()}` https://discordapp.com/channels/{e.GuildId}/{e.ChannelId}/{e.MessageId}");
                
                var embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle("List of Events")
                    .WithDescription($"{string.Join("\n", events)}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync(err).ConfigureAwait(false);
            }
        }
    }
}