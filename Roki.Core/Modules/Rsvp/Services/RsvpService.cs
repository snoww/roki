using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;

namespace Roki.Modules.Rsvp.Services
{
    public class RsvpService : IRService
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private Timer _timer;

        public RsvpService(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
            EventTimer();
        }

        private void EventTimer()
        {
            _timer = new Timer(EventUpdate, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
        }

        private async void EventUpdate(object state)
        {
            using var uow = _db.GetDbContext();
            var events = uow.Events.GetAllActiveEvents();
            if (events.Count == 0) return;
            foreach (var e in events)
            {
                var message = await _client.GetGuild(e.GuildId).GetTextChannel(e.ChannelId).GetMessageAsync(e.MessageId).ConfigureAwait(false) as IUserMessage;
                var old = message?.Embeds.First();
                if (old == null) continue;
                var part = old.Fields.First(f => f.Name.Contains("part", StringComparison.OrdinalIgnoreCase));
                var und = old.Fields.First(f => f.Name.Contains("unde", StringComparison.OrdinalIgnoreCase));
                var newEmbed = new EmbedBuilder().WithOkColor()
                    .WithAuthor(old.Author?.Name, old.Author?.Url)
                    .WithTitle(old.Title)
                    .AddField("Event Date", e.StartDate.ToString("f"))
                    .AddField(part.Name, part.Value)
                    .AddField(und.Name, und.Value)
                    .WithTimestamp(e.StartDate)
                    .WithDescription($"Starts in {(e.StartDate - DateTimeOffset.Now).ToReadableString()}`")
                    .WithFooter("Event starts");
                if (e.StartDate <= DateTimeOffset.Now)
                {
                    newEmbed.WithDescription("Event started")
                        .WithFooter("Event started");
                    await message.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
                    uow.Context.Events.Remove(e);
                    continue;
                }
                
                await message.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
            }
        }

        public async Task CreateEvent(ICommandContext ctx)
        {
            var q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup Step 1")
//                    .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription("Which channel should the event be in? (Defaults to <#217668245644247041>")
            ).ConfigureAwait(false);
            
            var replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            var eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
            while (eventChannel == null)
            {
                var err = await ctx.Channel.SendErrorAsync("No channel specified. Please specify the channel using #\ni.e. <#220571291617329154>").ConfigureAwait(false);
                err.DeleteAfter(60);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 1 minute. If you still want to setup and event, you must start over.");
                    return;
                }
                eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
            }
            
            var q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup Step 2")
                    .WithDescription($"Using channel {eventChannel} for the event.\nNow enter a title for the event."))
                .ConfigureAwait(false);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            var eventTitle = replyMessage.Content.TrimTo(250, true);
            
            var q3 =await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup Step 3")
                    .WithDescription($"The title of the event has been set to {Format.Bold(eventTitle)}\n" +
                                     $"Please enter a description for the event.\n"))
                .ConfigureAwait(false);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }
            var eventDesc = replyMessage.Content;
            
            var q4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup Step 3")
                    .WithDescription($"Now please enter the date and time when this event starts. Default timezone is `{TimeZoneInfo.Local.StandardName}` or `UTC`\n" +
                                     "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
                .ConfigureAwait(false);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            var eventDate = ParseDateTimeFromString(replyMessage.Content);
            while (eventDate == null || eventDate.Value <= DateTimeOffset.Now)
            {
                var err = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `nov 24 5pm`, `21:30 jan 19 2020 utc`").ConfigureAwait(false);
                err.DeleteAfter(60);
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
            
            var q4Confirm = new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup Step 3")
                .WithDescription($"Is the date correct (yes/no)?\n{dateStr}");
            await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
            var confirm = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (confirm == null)
            {
                await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                return;
            }

            while (!confirm.Content.Equals("yes") || !confirm.Content.Equals("y"))
            {
                var err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("RSVP Event Setup Step 3")
                        .WithDescription($"Please enter the date this event starts. Default timezone is `{TimeZoneInfo.Local.StandardName}` or `UTC`" +
                                         "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
                    .ConfigureAwait(false);
                err.DeleteAfter(60);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync("RSVP Event setup timed out.\nNo message received for 3 minutes. If you still want to setup and event, you must start over.");
                    return;
                }

                eventDate = ParseDateTimeFromString(replyMessage.Content);
                while (eventDate == null || eventDate.Value <= DateTimeOffset.Now)
                {
                    var err2 = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`").ConfigureAwait(false);
                    err2.DeleteAfter(60);
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
            
                q4Confirm = new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup Step 3")
                    .WithDescription($"Is the date correct (yes/no)?\n{dateStr}");
                var con = await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
                con.DeleteAfter(10);
            }

            var startsIn = eventDate.Value - DateTimeOffset.Now;
            var eventEmbed = new EmbedBuilder().WithOkColor()
                .WithTitle(eventTitle)
                .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription($"Starts in `{startsIn.ToReadableString()}`")
                .AddField("Event Date", eventDate.Value.ToString("f"))
                .AddField("Participants(0)", "```None```")
                .AddField("❔ Undecided", "```None```")
                .WithFooter("Event starts")
                .WithTimestamp(eventDate.Value);

            await q1.DeleteAsync().ConfigureAwait(false);
            await q2.DeleteAsync().ConfigureAwait(false);
            await q3.DeleteAsync().ConfigureAwait(false);
            await q4.DeleteAsync().ConfigureAwait(false);
            var rsvp = await eventChannel.EmbedAsync(eventEmbed).ConfigureAwait(false);
            
            using var uow = _db.GetDbContext();
            uow.Context.Events.Add(new Event
            {
                Name = eventTitle,
                Description = eventDesc,
                Host = ctx.User.Id,
                StartDate = eventDate.Value,
                GuildId = ctx.Guild.Id,
                ChannelId = eventChannel.Id,
                MessageId = rsvp.Id,
            });
            await uow.SaveChangesAsync().ConfigureAwait(false);
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