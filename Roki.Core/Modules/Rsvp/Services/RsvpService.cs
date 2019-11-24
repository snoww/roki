using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Rsvp.Services
{
    public class RsvpService : IRService
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;

        public RsvpService(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
        }

        public async Task CreateEvent(ICommandContext ctx)
        {
            var q1 = new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup Step 1")
//                    .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription("Which channel should the event be in? (Defaults to <#217668245644247041>");
            
            await ctx.Channel.EmbedAsync(q1).ConfigureAwait(false);
            
            var replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            var eventChannel = replyMessage.MentionedChannels.FirstOrDefault();
            while (eventChannel == null)
            {
                await ctx.Channel.SendErrorAsync("No channel specified. Please specify the channel using #\ni.e. <#220571291617329154>").ConfigureAwait(false);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 1 minute. If you still want to setup and event, you must start over.");
                    return;
                }
                eventChannel = replyMessage.MentionedChannels.FirstOrDefault();
            }

            var q2 = new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup Step 2")
                .WithDescription($"Using channel {eventChannel} for the event.\nNow enter a title for the event.");
            await ctx.Channel.EmbedAsync(q2).ConfigureAwait(false);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            var eventTitle = replyMessage.Content.TrimTo(250, true);
            var q3 = new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup Step 3")
                .WithDescription($"The title of the event has been set to {Format.Bold(eventTitle)}\n" +
                                 $"Now please enter when this event starts. Default timezone is `{TimeZoneInfo.Local.StandardName}` or `UTC`" +
                                 "Examples: `tomorrow 1pm`, `nov 24 5pm`, `21:30 jan 19 2020 utc`");
            await ctx.Channel.EmbedAsync(q3).ConfigureAwait(false);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            var eventDate = ParseDateTimeFromString(replyMessage.Content);
            while (eventDate == null)
            {
                await ctx.Channel.SendErrorAsync("Could not parse date time. Please try again.").ConfigureAwait(false);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 1 minute. If you still want to setup and event, you must start over.");
                    return;
                }
                eventDate = ParseDateTimeFromString(replyMessage.Content);
            }

            var dateStr = eventDate.Value.ToString("f");
            if (eventDate.Value.Offset == TimeSpan.Zero)
                dateStr += "UTC";
            
            var q3Confirm = new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup Step 3")
                .WithDescription($"Is the date correct (yes/no)?\n{dateStr}");
            await ctx.Channel.EmbedAsync(q3Confirm).ConfigureAwait(false);
            var confirm = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (confirm == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            while (!confirm.Content.Equals("yes") || !confirm.Content.Equals("y"))
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("RSVP Event Setup Step 3")
                        .WithDescription($"Please enter the date this event starts. Default timezone is `{TimeZoneInfo.Local.StandardName}` or `UTC`" +
                                         "Examples: `tomorrow 1pm`, `nov 24 5pm`, `21:30 jan 19 2020 utc`"))
                    .ConfigureAwait(false);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                    return;
                }

                eventDate = ParseDateTimeFromString(replyMessage.Content);
                while (eventDate == null)
                {
                    await ctx.Channel.SendErrorAsync("Could not parse date time. Please try again.").ConfigureAwait(false);
                    replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    if (replyMessage == null)
                    {
                        await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 1 minute. If you still want to setup and event, you must start over.");
                        return;
                    }
                    eventDate = ParseDateTimeFromString(replyMessage.Content);
                }

                dateStr = eventDate.Value.ToString("f");
                if (eventDate.Value.Offset == TimeSpan.Zero)
                    dateStr += "UTC";
            
                q3Confirm = new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup Step 3")
                    .WithDescription($"Is the date correct (yes/no)?\n{dateStr}");
                await ctx.Channel.EmbedAsync(q3Confirm).ConfigureAwait(false);
            }
            
            var eventEmbed = new EmbedBuilder().WithOkColor()
                .WithTitle(eventTitle)
                .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription($"Starts in ");
        }

        private async Task<SocketMessage> ReplyHandler(ICommandContext ctx, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(5);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != ctx.Channel.Id || message.Author.Id != ctx.User.Id) return Task.CompletedTask;
                eventTrigger.SetResult(message);
                return Task.CompletedTask;
            }
            
            _client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var cancel = cancelTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, cancel, delay).ConfigureAwait(false);

            _client.MessageReceived -= Handler;

            if (task == trigger)
                return await trigger.ConfigureAwait(false);
            return null;
        }

        private DateTimeOffset? ParseDateTimeFromString(string datetime)
        {
            if (datetime.Contains("tomorrow", StringComparison.OrdinalIgnoreCase))
            {
                var tmr = DateTimeOffset.Now.AddDays(1);
                datetime.Replace("tomorrow", tmr.ToString("d"), StringComparison.OrdinalIgnoreCase);
            }

            try
            {
                return DateTimeOffset.Parse(datetime);
            }
            catch
            {
                return null;
            }
        }
    }
}