using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;

namespace Roki.Modules.Rsvp.Services
{
    public class RsvpService : IRService
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private Timer _timer;
        private static readonly IEmote Confirm = new Emoji("✅");
        private static readonly IEmote Cancel = new Emoji("❌");
        private static readonly IEmote Uncertain = new Emoji("❔");
        private const string Error = "RSVP Event setup timed out. No message received.\nIf you still want to setup and event, you must start over.";
        private const string Stop = "RSVP Event setup canceled.\nIf you still want to setup and event, you must start over.";
        private const string ErrorEdit = "RSVP Event edit cancelled. No message received.";
        private const string StopEdit = "RSVP Event edit stopped.";

        private static readonly IEmote[] Choices =
        {
            Confirm,
            Cancel,
            Uncertain
        };

        public RsvpService(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
            _client.ReactionAdded += ReactionHandler;
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
                    .WithAuthor(old.Author?.Name, old.Author?.IconUrl)
                    .WithTitle(old.Title)
                    .AddField("Description", e.Description)
                    .AddField("Event Date", $"`{e.StartDate:f}`")
                    .AddField(part.Name, part.Value)
                    .AddField(und.Name, und.Value)
                    .WithTimestamp(e.StartDate)
                    .WithDescription($"Starts in `{(e.StartDate - DateTimeOffset.Now).ToReadableString()}`")
                    .WithFooter("Event starts");
                if (e.StartDate <= DateTimeOffset.Now)
                {
                    newEmbed.WithDescription("Event started")
                        .WithFooter("Event started");
                    await message.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
                    uow.Context.Events.Remove(e);
                    await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                    continue;
                }
                
                await message.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task CreateEvent(ICommandContext ctx)
        {
            var toDelete = new List<IUserMessage> {ctx.Message};
            var q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup - Step 1")
                .WithDescription("Which **channel** should the event be in?\n`this`, `current` or `here` for current channel, `default` for default channel\n(default is <#217668245644247041>)")
                .WithFooter("Type stop to cancel event setup")
            ).ConfigureAwait(false);
            toDelete.Add(q1);
            var replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            toDelete.Add(replyMessage as IUserMessage);
            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            { 
                await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                return;
            }
            IMessageChannel eventChannel = null;
            if (replyMessage.MentionedChannels.Count > 1)
                eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
            else if (replyMessage.Content.Contains("this", StringComparison.OrdinalIgnoreCase) || 
                replyMessage.Content.Contains("current", StringComparison.OrdinalIgnoreCase) || 
                replyMessage.Content.Contains("here", StringComparison.OrdinalIgnoreCase))
                eventChannel = ctx.Channel;
            else if (replyMessage.Content.Contains("default", StringComparison.OrdinalIgnoreCase))
                eventChannel = _client.GetChannel(217668245644247041) as IMessageChannel;
            while (eventChannel == null)
            {
                var err = await ctx.Channel.SendErrorAsync("No channel specified. Please specify the channel using #\ni.e. <#217668245644247041>").ConfigureAwait(false);
                toDelete.Add(err);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(Error);
                    return;
                }
                toDelete.Add(replyMessage as IUserMessage);
                if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                    await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                    return;
                }
                if (replyMessage.MentionedChannels.Count > 1)
                    eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
                else if (replyMessage.Content.Contains("this", StringComparison.OrdinalIgnoreCase) || 
                    replyMessage.Content.Contains("current", StringComparison.OrdinalIgnoreCase) || 
                    replyMessage.Content.Contains("here", StringComparison.OrdinalIgnoreCase))
                    eventChannel = ctx.Channel;
                else if (replyMessage.Content.Contains("default", StringComparison.OrdinalIgnoreCase))
                    eventChannel = _client.GetChannel(217668245644247041) as IMessageChannel;
            }

            var q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 2")
                    .WithDescription($"Using <#{eventChannel.Id}> for the event.\nNow enter a **title** for the event.")
                    .WithFooter("Type stop to cancel event setup"))
                .ConfigureAwait(false);
            toDelete.Add(q2);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            toDelete.Add(replyMessage as IUserMessage);
            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                return;
            }
            var eventTitle = replyMessage.Content.TrimTo(250, true);
            
            var q3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 3")
                    .WithDescription($"The title of the event has been set to: {Format.Bold(eventTitle)}\n" +
                                     $"Please enter a **description** for the event.\n")
                    .WithFooter("Type stop to cancel event setup"))
                .ConfigureAwait(false);
            toDelete.Add(q3);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            var eventDesc = replyMessage.Content.TrimTo(1000, true);
            toDelete.Add(replyMessage as IUserMessage);
            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                return;
            }
            
            var q4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 4")
                    .WithDescription($"Now please enter the date and time when this event starts.\nDefault timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`\n" +
                                     "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`")
                    .WithFooter("Type stop to cancel event setup"))
                .ConfigureAwait(false);
            toDelete.Add(q4);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            var eventDate = ParseDateTimeFromString(replyMessage.Content);
            toDelete.Add(replyMessage as IUserMessage);
            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                return;
            }
            while (eventDate == null || eventDate.Value <= DateTimeOffset.Now)
            {
                var err = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`").ConfigureAwait(false);
                toDelete.Add(err);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(Error);
                    return;
                }
                eventDate = ParseDateTimeFromString(replyMessage.Content);
                toDelete.Add(replyMessage as IUserMessage);
                if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                    await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                    return;
                }
            }
            var dateStr = eventDate.Value.ToString("f");
            if (eventDate.Value.Offset == TimeSpan.Zero)
                dateStr += "UTC";
            var q4Confirm = new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup - Step 4")
                .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`")
                .WithFooter("Type stop to cancel event setup");
            var conf = await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
            toDelete.Add(conf);
            var confirm = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (confirm == null)
            {
                await ctx.Channel.SendErrorAsync(Error).ConfigureAwait(false);
                return;
            }
            toDelete.Add(confirm as IUserMessage);
            if (confirm.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                return;
            }
            while (!confirm.Content.Contains("yes", StringComparison.OrdinalIgnoreCase) || !confirm.Content.Contains("y", StringComparison.OrdinalIgnoreCase))
            {
                var err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("RSVP Event Setup - Step 4")
                        .WithDescription($"Please enter the date this event starts. Default timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`" +
                                         "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`")
                        .WithFooter("Type stop to cancel event setup"))
                    .ConfigureAwait(false);
                toDelete.Add(err);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(Error).ConfigureAwait(false);
                    return;
                }

                eventDate = ParseDateTimeFromString(replyMessage.Content);
                toDelete.Add(replyMessage as IUserMessage);
                if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                    await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                    return;
                }
                while (eventDate == null || eventDate.Value <= DateTimeOffset.Now)
                {
                    var err2 = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`").ConfigureAwait(false);
                    toDelete.Add(err2);
                    replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    if (replyMessage == null)
                    {
                        await ctx.Channel.SendErrorAsync(Error);
                        return;
                    }
                    eventDate = ParseDateTimeFromString(replyMessage.Content);
                    toDelete.Add(replyMessage as IUserMessage);
                    if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                        await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                        return;
                    }
                }
                dateStr = eventDate.Value.ToString("f");
                if (eventDate.Value.Offset == TimeSpan.Zero)
                    dateStr += "UTC";
                
                q4Confirm = new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 4")
                    .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`")
                    .WithFooter("Type stop to cancel event setup");
                var con = await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
                toDelete.Add(con);
                confirm = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (confirm == null)
                {
                    await ctx.Channel.SendErrorAsync(Error);
                    return;
                }
                toDelete.Add(confirm as IUserMessage);
                if (confirm.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                    await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                    return;
                }
            }
            
            using var uow = _db.GetDbContext();
            var ev = uow.Context.Events.Add(new Event
            {
                Name = eventTitle,
                Description = eventDesc,
                Host = ctx.User.Id,
                StartDate = eventDate.Value,
                GuildId = ctx.Guild.Id,
                ChannelId = eventChannel.Id,
            });
            await uow.SaveChangesAsync().ConfigureAwait(false);

            var startsIn = eventDate.Value - DateTimeOffset.Now;
            var eventEmbed = new EmbedBuilder().WithOkColor()
                .WithTitle($"#{ev.Entity.Id}: {eventTitle}")
                .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription($"Starts in `{startsIn.ToReadableString()}`")
                .AddField("Description", eventDesc)
                .AddField("Event Date", $"`{eventDate.Value:f}`")
                .AddField("Participants (0)", "```None```")
                .AddField("Undecided", "```None```")
                .WithFooter("Event starts")
                .WithTimestamp(eventDate.Value);

            await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
            IUserMessage rsvp;
            try
            {
                rsvp = await eventChannel.EmbedAsync(eventEmbed).ConfigureAwait(false);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync($"Insufficient Permissions in <#{eventChannel.Id}>");
                return;
            }

            ev.Entity.MessageId = rsvp.Id;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await rsvp.AddReactionsAsync(Choices).ConfigureAwait(false);
        }

        public async Task EditEvent(ICommandContext ctx)
        {
            var toDelete = new List<IUserMessage> {ctx.Message};
            var events = new List<Event>();
            var formattedEvents = events.Select(e => $"`#{e.Id}`: {e.Name} `{e.StartDate:f}`");
            using var uow = _db.GetDbContext();
            events = uow.Context.Events.Where(e => e.Host == ctx.User.Id).ToList();

            if (events.Count == 0)
            {
                await ctx.Channel.SendErrorAsync("You do not have any active events.").ConfigureAwait(false);
                return;
            }

            SocketMessage replyMessage;
            var ev = events.First();
            if (events.Count > 1)
            {
                var q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Editor")
                    .WithDescription($"Which Event would you like to edit? Type in the # to select.\n{string.Join("\n", formattedEvents)}")
                    .WithFooter("Type stop to cancel event edit")
                ).ConfigureAwait(false);
                toDelete.Add(q1);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(ErrorEdit);
                    return;
                }
                toDelete.Add(replyMessage as IUserMessage);
                if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                { 
                    await ctx.Channel.SendErrorAsync(StopEdit).ConfigureAwait(false);
                    await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                    return;
                }

                ev = events.FirstOrDefault(e => e.Id.ToString().Equals(replyMessage.Content, StringComparison.OrdinalIgnoreCase));
                while (ev == null)
                {
                    var err = await ctx.Channel.SendErrorAsync("Cannot find an event with that ID, please try again.").ConfigureAwait(false);
                    toDelete.Add(err);
                    replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    if (replyMessage == null)
                    {
                        await ctx.Channel.SendErrorAsync(ErrorEdit);
                        return;
                    }
                    toDelete.Add(replyMessage as IUserMessage);
                    if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    { 
                        await ctx.Channel.SendErrorAsync(StopEdit).ConfigureAwait(false);
                        await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                        return;
                    }
                    ev = events.FirstOrDefault(e => e.Id.ToString().Equals(replyMessage.Content, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            var q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Editor")
                .WithDescription($"What do you want to change for: #{ev.Id} {ev.Name}\n1. Edit Title\n2. Edit Description\n3. Edit Event Date\n4. Delete Event")
                .WithFooter("Type stop to finish editing")
            ).ConfigureAwait(false);
            toDelete.Add(q2);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(ErrorEdit);
                return;
            }
            toDelete.Add(replyMessage as IUserMessage);
            var stop = false;
            while (!replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase) || stop)
            {
                // Edit Title
                SocketMessage editReply;
                if (replyMessage.Content.StartsWith("1", StringComparison.OrdinalIgnoreCase))
                {
                    var e1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Edit Title")
                            .WithDescription("What should the new **title** be?"))
                        .ConfigureAwait(false);
                    toDelete.Add(e1);
                    editReply = await ReplyHandler(ctx).ConfigureAwait(false);
                    if (editReply == null)
                    {
                        await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                        return;
                    }
                    toDelete.Add(editReply as IUserMessage);
                    if (editReply.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        stop = true;
                        continue;
                    }

                    ev.Name = editReply.Content.TrimTo(250, true);
                    var er1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Edit Title")
                            .WithDescription($"The event title has been set to: **{ev.Name}**"))
                        .ConfigureAwait(false);
                    toDelete.Add(er1);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
                // Edit Description
                else if (replyMessage.Content.StartsWith("2", StringComparison.OrdinalIgnoreCase))
                {
                    var e2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Edit Description")
                            .WithDescription("What should the new **description** be?"))
                        .ConfigureAwait(false);
                    toDelete.Add(e2);
                    editReply = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                    if (editReply == null)
                    {
                        await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                        return;
                    }
                    toDelete.Add(editReply as IUserMessage);
                    if (editReply.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        stop = true;
                        continue;
                    }

                    ev.Description = editReply.Content.TrimTo(1000, true);
                    var er2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Edit Description")
                            .WithDescription($"The event description has been changed to:\n{ev.Description}"))
                        .ConfigureAwait(false);
                    toDelete.Add(er2);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
                // Edit Event Date
                else if (replyMessage.Content.StartsWith("3", StringComparison.OrdinalIgnoreCase))
                {
                    var e3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Edit Event Date")
                            .WithDescription("What should the new event date be?" +
                                             $"Default timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`" +
                                             "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
                        .ConfigureAwait(false);
                    toDelete.Add(e3);
                    editReply = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                    if (editReply == null)
                    {
                        await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                        return;
                    }
                    toDelete.Add(editReply as IUserMessage);
                    if (editReply.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        stop = true;
                        continue;
                    }
                    var newDate = ParseDateTimeFromString(editReply.Content);
                    while (newDate == null || newDate.Value <= DateTimeOffset.Now)
                    {
                        var err = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`").ConfigureAwait(false);
                        toDelete.Add(err);
                        replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                        if (replyMessage == null)
                        {
                            await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                            return;
                        }
                        newDate = ParseDateTimeFromString(replyMessage.Content);
                        toDelete.Add(replyMessage as IUserMessage);
                        if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            stop = true;
                            break;
                        }
                    }
                    if (stop) continue;
                    
                    var dateStr = newDate.Value.ToString("f");
                    if (newDate.Value.Offset == TimeSpan.Zero)
                        dateStr += "UTC";

                    var econ3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Edit Event Date")
                            .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`"))
                        .ConfigureAwait(false);
                    toDelete.Add(econ3);
                    var con = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                    if (con == null)
                    {
                        await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                        return;
                    }
                    toDelete.Add(con as IUserMessage);
                    if (con.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        stop = true;
                        continue;
                    }

                    while (!con.Content.Contains("yes", StringComparison.OrdinalIgnoreCase) || !con.Content.Contains("y", StringComparison.OrdinalIgnoreCase))
                    {
                        var err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("RSVP Event Editor - Edit Event Date")
                                .WithDescription(
                                    $"Please enter the correct date. Default timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`" +
                                    "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
                            .ConfigureAwait(false);
                        toDelete.Add(err);
                        replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                        if (replyMessage == null)
                        {
                            await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                            return;
                        }

                        newDate = ParseDateTimeFromString(replyMessage.Content);
                        toDelete.Add(replyMessage as IUserMessage);
                        if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            stop = true;
                            break;
                        }
                        while (newDate == null || newDate.Value <= DateTimeOffset.Now)
                        {
                            var err2 = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`").ConfigureAwait(false);
                            toDelete.Add(err2);
                            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                            if (replyMessage == null)
                            {
                                await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                                return;
                            }
                            newDate = ParseDateTimeFromString(replyMessage.Content);
                            toDelete.Add(replyMessage as IUserMessage);
                            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                            {
                                stop = true;
                                break;
                            }
                        }
                        if (stop) continue;
                    
                        dateStr = newDate.Value.ToString("f");
                        if (newDate.Value.Offset == TimeSpan.Zero)
                            dateStr += "UTC";

                        var con2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                                .WithTitle("RSVP Event Editor - Edit Event Date")
                                .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`"))
                            .ConfigureAwait(false);
                        toDelete.Add(con2);
                        con = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                        if (con == null)
                        {
                            await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                            return;
                        }
                        toDelete.Add(con as IUserMessage);
                        if (con.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            stop = true;
                            break;
                        }
                    }
                    if (stop) continue;
                    ev.StartDate = newDate.Value;
                    var er2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Edit Event Date")
                            .WithDescription($"The event date has been changed to:\n`{ev.StartDate:f}`"))
                        .ConfigureAwait(false);
                    toDelete.Add(er2);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
                // Delete
                else if (replyMessage.Content.StartsWith("4", StringComparison.OrdinalIgnoreCase))
                {
                    var e4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("RSVP Event Editor - Delete Event")
                            .WithDescription($"Are you sure you want to delete the event? `yes`/`no`\n#{ev.Id} {ev.Name}\n**You cannot undo this process.**"))
                        .ConfigureAwait(false);
                    toDelete.Add(e4);
                    replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                    if (replyMessage == null)
                    {
                        await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                        return;
                    }

                    if (replyMessage.Content.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                        replyMessage.Content.Equals("y", StringComparison.OrdinalIgnoreCase))
                    {
                        var message = await _client.GetGuild(ev.GuildId).GetTextChannel(ev.ChannelId).GetMessageAsync(ev.MessageId).ConfigureAwait(false) as IUserMessage;
                        uow.Context.Events.Remove(ev);
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                        try
                        {
                            await message.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                                .WithTitle("RSVP Event Editor - Event Deleted")
                                .WithDescription($"#{ev.Id} {ev.Name} has been deleted"))
                            .ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    var err = await ctx.Channel.SendErrorAsync("Unknown Option, please select a valid option.").ConfigureAwait(false);
                    toDelete.Add(err);
                }
                var q2Repeat = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Editor")
                    .WithDescription($"What else do you want to change for: #{ev.Id} {ev.Name}\n1. Edit Title\n2. Edit Description\n3. Edit Start Date\n4. Delete Event")
                    .WithFooter("Type stop to finish editing")
                ).ConfigureAwait(false);
                toDelete.Add(q2Repeat);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                    await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                    return;
                }
            }
            await ctx.Channel.SendErrorAsync(StopEdit).ConfigureAwait(false);
            await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Editor")
                    .WithDescription("The event editor has been stopped. Any changes you've made have been saved."))
                .ConfigureAwait(false);
        }

        public List<Event> ListEvents(ulong guildId, int page = 0)
        {
            var uow = _db.GetDbContext();
            return uow.Context.Events.Where(e => e.GuildId == guildId)
                .OrderByDescending(e => e.StartDate)
                .Skip(page * 9)
                .Take(9)
                .ToList();
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

        private async Task ReactionHandler(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channel, SocketReaction r)
        {
            if (r.User.Value.IsBot) return;
            using var uow = _db.GetDbContext();
            var events = uow.Events.GetAllActiveEvents();
            var messageIds = events.Select(e => e.MessageId).ToList();
            
            if (!messageIds.Contains(r.MessageId) || !Choices.Contains(r.Emote)) return;

            var msg = await cache.DownloadAsync().ConfigureAwait(false);
            var old = msg.Embeds.First();
            if (old == null) return;
            
            var date = old.Fields.First(f => f.Name.Contains("date", StringComparison.OrdinalIgnoreCase));

            var ev = events.First(e => e.MessageId == r.MessageId);

            var newEmbed = new EmbedBuilder().WithOkColor()
                .WithAuthor(old.Author?.Name, old.Author?.IconUrl)
                .WithTitle(old.Title)
                .AddField("Description", ev.Description)
                .AddField("Event Date", date.Value)
                .WithTimestamp(old.Timestamp.GetValueOrDefault())
                .WithDescription($"Starts in `{(old.Timestamp.GetValueOrDefault() - DateTimeOffset.Now).ToReadableString()}`")
                .WithFooter("Event starts");

            var und = new List<string>();
            var par = new List<string>();
            if (!string.IsNullOrWhiteSpace(ev.Undecided))
                und = ev.Undecided.Split('\n').ToList();
            if (!string.IsNullOrWhiteSpace(ev.Participants))
                par = ev.Participants.Split('\n').ToList();

            if (r.Emote.Equals(Confirm))
            {
                await msg.RemoveReactionAsync(Cancel, r.User.Value).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Uncertain, r.User.Value).ConfigureAwait(false);
                if (und.Contains(r.User.Value.ToString())) und.Remove(r.User.Value.ToString());
                if (par.Contains(r.User.Value.ToString())) return;
                par.Add(r.User.Value.ToString());
            }
            else if (r.Emote.Equals(Cancel))
            {
                await msg.RemoveReactionAsync(Confirm, r.User.Value).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Uncertain, r.User.Value).ConfigureAwait(false);
                if (und.Contains(r.User.Value.ToString())) und.Remove(r.User.Value.ToString());
                if (par.Contains(r.User.Value.ToString())) par.Remove(r.User.Value.ToString());
            }
            else 
            {
                await msg.RemoveReactionAsync(Cancel, r.User.Value).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Confirm, r.User.Value).ConfigureAwait(false);
                if (par.Contains(r.User.Value.ToString())) par.Remove(r.User.Value.ToString());
                if (und.Contains(r.User.Value.ToString())) return;
                und.Add(r.User.Value.ToString());
            }

            ev.Participants = string.Join('\n', par);
            ev.Undecided = string.Join('\n', und);
            if (par.Count == 0)
                newEmbed.AddField($"Participants ({par.Count})", "```None```");
            else
                newEmbed.AddField($"Participants ({par.Count})", $"```{ev.Participants}```");
            if (und.Count == 0)
                newEmbed.AddField($"Undecided", "```None```");
            else
                newEmbed.AddField($"Undecided ({und.Count})", $"```{ev.Undecided}```");
            
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await msg.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
        }

        private DateTimeOffset? ParseDateTimeFromString(string datetime)
        {
            if (datetime.Contains("tomorrow", StringComparison.OrdinalIgnoreCase))
            {
                var tmr = DateTimeOffset.Now.AddDays(1);
                datetime = datetime.Replace("tomorrow", tmr.ToString("d"), StringComparison.OrdinalIgnoreCase);
            } 
            else if (datetime.Contains("tmr", StringComparison.OrdinalIgnoreCase))
            {
                var tmr = DateTimeOffset.Now.AddDays(1);
                datetime = datetime.Replace("tmr", tmr.ToString("d"), StringComparison.OrdinalIgnoreCase);
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