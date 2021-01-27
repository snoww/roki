using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using NLog;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Rsvp.Services
{
    public class RsvpService : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private readonly IMongoService _mongo;
        private readonly IConfigurationService _config;
        private readonly ConcurrentDictionary<ObjectId, Event> _activeReminders = new();
        private Timer _timer;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
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

        public RsvpService(DiscordSocketClient client, IMongoService mongo, IConfigurationService config)
        {
            _client = client;
            _mongo = mongo;
            _config = config;
            _client.ReactionAdded += ReactionHandler;
            EventTimer();
        }

        private void EventTimer()
        {
            _timer = new Timer(EventUpdate, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
        }

        private async void EventUpdate(object state)
        {
            var events = await _mongo.Context.GetActiveEventsAsync().ConfigureAwait(false);
            if (events.Count == 0) return;

            try
            {
                var now = DateTime.UtcNow;
                foreach (var e in events)
                {
                    var message = await _client.GetGuild(e.GuildId).GetTextChannel(e.ChannelId).GetMessageAsync(e.MessageId).ConfigureAwait(false) as IUserMessage;
                    var old = message?.Embeds.First();
                    if (old == null) continue;
                    var part = old.Fields.First(f => f.Name.StartsWith("P", StringComparison.Ordinal));
                    var und = old.Fields.First(f => f.Name.StartsWith("U", StringComparison.Ordinal));
                    var newEmbed = new EmbedBuilder().WithDynamicColor(e.GuildId)
                        .WithAuthor(old.Author?.Name, old.Author?.IconUrl)
                        .WithTitle($"`{e.Id.GetHexId()}` {e.Name}")
                        .AddField("Description", e.Description)
                        .AddField("Event Date", $"```{e.StartDate:f} UTC```See footer for local time.")
                        .AddField(part.Name, part.Value)
                        .AddField(und.Name, und.Value)
                        .WithTimestamp(e.StartDate)
                        .WithDescription($"Starts in```{(e.StartDate - now).ToReadableString()}```")
                        .WithFooter("Event starts");
                    if (!_activeReminders.ContainsKey(e.Id) && e.StartDate.AddMinutes(-45) <= now)
                    {
                        _activeReminders.TryAdd(e.Id, e);
                        Logger.Info("Event {name} reminder countdown has started", e.Name);
                        await StartEventCountdowns(e).ConfigureAwait(false);
                    }
                    if (e.StartDate <= now)
                    {
                        newEmbed.WithDescription("Event started")
                            .WithFooter("Event started");
                        await message.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
                        _activeReminders.Remove(e.Id, out _);
                        await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                        continue;
                    }
                    
                    await message.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public async Task CreateEvent(ICommandContext ctx)
        {
            var toDelete = new List<IUserMessage> {ctx.Message};
            var q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                .WithTitle("RSVP Event Setup - Step 1")
                .WithDescription("Which **channel** should the event be in?\n`this`, `current` or `here` for current channel, `default` for default channel\n(default is <#217668245644247041>)") // temp default need to change later
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

            var q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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
            
            var q3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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
            
            var q4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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
            var q4Confirm = new EmbedBuilder().WithDynamicColor(ctx)
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
                var err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithTitle("RSVP Event Setup - Step 4")
                        .WithDescription($"Please enter the date this event starts. Default timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`\n" +
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
                
                q4Confirm = new EmbedBuilder().WithDynamicColor(ctx)
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

            var ev = new Event
            {
                Id = ObjectId.GenerateNewId(),
                Name = eventTitle,
                Description = eventDesc,
                Host = ctx.User.Id,
                StartDate = eventDate.Value.UtcDateTime,
                GuildId = ctx.Guild.Id,
                ChannelId = eventChannel.Id,
            };
            
            var startsIn = eventDate.Value - DateTimeOffset.Now;
            var eventEmbed = new EmbedBuilder().WithDynamicColor(ctx)
                .WithTitle($"`{ev.Id.GetHexId()}` {eventTitle}")
                .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription($"Starts in```{startsIn.ToReadableString()}```")
                .AddField("Description", eventDesc)
                .AddField("Event Date", $"```{eventDate.Value.ToUniversalTime():f} UTC```See footer for local time.")
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

            ev.MessageId = rsvp.Id;
            await _mongo.Context.AddNewEventAsync(ev).ConfigureAwait(false);

            await rsvp.AddReactionsAsync(Choices).ConfigureAwait(false);
        }

        public async Task EditEvent(ICommandContext ctx)
        {
            var toDelete = new List<IUserMessage> {ctx.Message};
            var events = await _mongo.Context.GetActiveEventsByHostAsync(ctx.User.Id).ConfigureAwait(false);

            if (events.Count == 0)
            {
                await ctx.Channel.SendErrorAsync("You do not have any active events.").ConfigureAwait(false);
                return;
            }

            SocketMessage replyMessage;
            var activeEditEvent = events.First();
            if (events.Count > 1)
            {
                var q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithTitle("RSVP Event Editor")
                    .WithDescription("Which Event would you like to edit? Type in the # to select.\n" +
                                     $"{string.Join("\n", events.Select(e => $"`{e.Id.GetHexId()}`: {e.Name} `{e.StartDate:f}`"))}")
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

                activeEditEvent = events.FirstOrDefault(e => e.Id.GetHexId().Equals(replyMessage.Content, StringComparison.OrdinalIgnoreCase));
                while (activeEditEvent == null)
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
                    activeEditEvent = events.FirstOrDefault(e => e.Id.GetHexId().Equals(replyMessage.Content, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            var q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                .WithTitle("RSVP Event Editor")
                .WithDescription($"What do you want to change for: `{activeEditEvent.Id.GetHexId()}` **{activeEditEvent.Name}**\n`1.` Edit Title\n`2.` Edit Description\n`3.` Edit Event Date\n`4.` Delete Event")
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
                    var e1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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

                    var update = Builders<Event>.Update.Set(x => x.Name, editReply.Content.TrimTo(250, true));
                    await _mongo.Context.UpdateEventAsync(activeEditEvent.Id, update);
                    
                    var er1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                            .WithTitle("RSVP Event Editor - Edit Title")
                            .WithDescription($"The event title has been set to: **{activeEditEvent.Name}**"))
                        .ConfigureAwait(false);
                    toDelete.Add(er1);
                }
                // Edit Description
                else if (replyMessage.Content.StartsWith("2", StringComparison.OrdinalIgnoreCase))
                {
                    var e2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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

                    var update = Builders<Event>.Update.Set(x => x.Description, editReply.Content.TrimTo(1000, true));
                    await _mongo.Context.UpdateEventAsync(activeEditEvent.Id, update);
                    
                    var er2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                            .WithTitle("RSVP Event Editor - Edit Description")
                            .WithDescription($"The event description has been changed to:\n{activeEditEvent.Description}"))
                        .ConfigureAwait(false);
                    toDelete.Add(er2);
                }
                // Edit Event Date
                else if (replyMessage.Content.StartsWith("3", StringComparison.OrdinalIgnoreCase))
                {
                    var e3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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

                    var econ3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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
                        var err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithTitle("RSVP Event Editor - Edit Event Date")
                                .WithDescription(
                                    $"Please enter the correct date. Default timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`\n" +
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

                        var con2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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
                    if (stop) 
                        continue;
                    
                    var update = Builders<Event>.Update.Set(x => x.StartDate, newDate.Value.UtcDateTime);
                    await _mongo.Context.UpdateEventAsync(activeEditEvent.Id, update);
                    
                    var er2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                            .WithTitle("RSVP Event Editor - Edit Event Date")
                            .WithDescription($"The event date has been changed to:\n`{activeEditEvent.StartDate:f}` UTC"))
                        .ConfigureAwait(false);
                    toDelete.Add(er2);
                }
                // Delete
                else if (replyMessage.Content.StartsWith("4", StringComparison.OrdinalIgnoreCase))
                {
                    var e4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                            .WithTitle("RSVP Event Editor - Delete Event")
                            .WithDescription($"Are you sure you want to delete the event? `yes`/`no`\n`{activeEditEvent.Id.GetHexId()}` **{activeEditEvent.Name}**\n**You cannot undo this process.**"))
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
                        var message = await _client.GetGuild(activeEditEvent.GuildId).GetTextChannel(activeEditEvent.ChannelId).GetMessageAsync(activeEditEvent.MessageId).ConfigureAwait(false) as IUserMessage;

                        var update = Builders<Event>.Update.Set(x => x.Deleted, true);
                        await _mongo.Context.UpdateEventAsync(activeEditEvent.Id, update).ConfigureAwait(false);
                        
                        try
                        {
                            await message.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Event Deleted")
                                .WithDescription($"`{activeEditEvent.Id.GetHexId()}` **{activeEditEvent.Name}** has been deleted"))
                            .ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    var err = await ctx.Channel.SendErrorAsync("Unknown Option, please select a valid option.").ConfigureAwait(false);
                    toDelete.Add(err);
                }
                
                // get updated event
                activeEditEvent = events.First(e => e.Id == activeEditEvent.Id);

                var q2Repeat = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithTitle("RSVP Event Editor")
                    .WithDescription($"What else do you want to change for: `{activeEditEvent.Id.GetHexId()}` **{activeEditEvent.Name}**\n1. Edit Title\n2. Edit Description\n3. Edit Start Date\n4. Delete Event")
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
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithTitle("RSVP Event Editor")
                    .WithDescription("The event editor has been stopped. Any changes you've made have been saved."))
                .ConfigureAwait(false);
        }

        private async Task<SocketMessage> ReplyHandler(ICommandContext ctx, TimeSpan? timeout = null)
        {
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(ctx.Guild.Id);

            timeout ??= TimeSpan.FromMinutes(5);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != ctx.Channel.Id || message.Author.Id != ctx.User.Id) 
                    return Task.CompletedTask;
                if (message.Content.StartsWith(guildConfig.Prefix)) // ignore commands
                    return Task.CompletedTask;
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
            if (r.User.Value.IsBot || !Choices.Contains(r.Emote) || !_mongo.Context.GetActiveEventAsync(r.MessageId, out var ev)) 
                return;
            
            var msg = await cache.DownloadAsync().ConfigureAwait(false);
            var old = msg.Embeds.First();
            if (old == null) 
                return;
            
            var newEmbed = new EmbedBuilder().WithDynamicColor(((IGuildChannel) channel).GuildId)
                .WithAuthor(old.Author?.Name, old.Author?.IconUrl)
                .WithTitle(ev.Name)
                .AddField("Description", ev.Description)
                .AddField("Event Date", $"```{ev.StartDate:f} UTC```See footer for local time.")
                .WithTimestamp(ev.StartDate)
                .WithDescription($"Starts in```{(ev.StartDate - DateTime.UtcNow).ToReadableString()}```")
                .WithFooter("Event starts");

            var user = r.User.Value;
            var username = user.ToString();
            var und = ev.Undecided;
            var par = ev.Participants;

            if (r.Emote.Equals(Confirm))
            {
                await msg.RemoveReactionAsync(Cancel, user).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Uncertain, user).ConfigureAwait(false);
                if (par.Contains(username)) 
                    return;
                if (ev.Undecided.Contains(username))
                {
                    und.Remove(username);
                }
                par.Add(username);
            }
            else if (r.Emote.Equals(Cancel))
            {
                await msg.RemoveReactionAsync(Confirm, user).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Uncertain, user).ConfigureAwait(false);
                if (und.Contains(username)) 
                    und.Remove(username);
                if (par.Contains(username)) 
                    par.Remove(username);
            }
            else 
            {
                await msg.RemoveReactionAsync(Cancel, user).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Confirm, user).ConfigureAwait(false);
                if (und.Contains(username))
                    return;
                if (par.Contains(username)) 
                    par.Remove(username);
                und.Add(username);
            }

            var update = Builders<Event>.Update.Set(x => x.Participants, par).Set(x => x.Undecided, und);
            await _mongo.Context.UpdateEventAsync(ev.Id, update).ConfigureAwait(false);

            if (par.Count == 0)
                newEmbed.AddField($"Participants ({par.Count})", "```None```");
            else
                newEmbed.AddField($"Participants ({par.Count})", $"```{string.Join("\n", par)}```");
            
            if (und.Count == 0)
                newEmbed.AddField($"Undecided", "```None```");
            else
                newEmbed.AddField($"Undecided ({und.Count})", $"```{string.Join("\n", und)}```");
            
            await msg.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
        }

        private static DateTimeOffset? ParseDateTimeFromString(string datetime)
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

        private Task StartEventCountdowns(Event e)
        {
            var thirtyMinutes = e.StartDate.AddMinutes(-30) - DateTime.UtcNow;
            var startTime = e.StartDate - DateTime.UtcNow;

            if (thirtyMinutes >= TimeSpan.Zero)
            {
                Logger.Info("Event {name}: sending 30 minute reminder in {thirtymin}", e.Name, startTime);
                SendNotification(e.Id, thirtyMinutes, NotificationType.ThirtyMinutes);
            }

            if (startTime >= TimeSpan.Zero)
            {
                Logger.Info("Event {name}: sending start reminder in {starttime}", e.Name, startTime);
                SendNotification(e.Id, startTime, NotificationType.Starting);
            }
            
            return Task.CompletedTask;
        }

        private void SendNotification(ObjectId id, TimeSpan delay, NotificationType type)
        {
            var _ = Task.Run(async () =>
            {
                await Task.Delay(delay).ConfigureAwait(false);

                var latestEvent = await _mongo.Context.GetEventByIdAsync(id).ConfigureAwait(false);
                
                if (latestEvent.Participants.Count == 0)
                    return;

                var participants = latestEvent.Participants;
                foreach (var par in participants)
                {
                    try
                    {
                        var split = par.LastIndexOf("#", StringComparison.Ordinal);
                        var username = par.Substring(0, split);
                        var discrim = par.Substring(split + 1);
                        var user = _client.GetUser(username, discrim);
                        var guild = _client.GetGuild(latestEvent.GuildId);
                        var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        var embed = new EmbedBuilder().WithOkColor()
                            .WithFooter($"From server: {guild.Name}");

                        if (type == NotificationType.Starting)
                        {
                            embed.WithTitle($"{latestEvent.Name} starting now!")
                                .WithDescription($"The event you registered for: {latestEvent.Name} is starting now!");
                        }
                        else
                        {
                            embed.WithTitle($"{latestEvent.Name} reminder")
                                .WithDescription($"The event you registered for: {latestEvent.Name} is starting in 30 minutes!");
                        }
                        
                        await dm.EmbedAsync(embed).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }
            });
        }

        public async Task<List<Event>> GetActiveGuildEvents(ulong guildId)
        {
            return await _mongo.Context.GetActiveEventsByGuild(guildId).ConfigureAwait(false);
        }

        private enum NotificationType
        {
            ThirtyMinutes,
            Starting
        }
    }
}