using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using StackExchange.Redis;
using IDatabase = StackExchange.Redis.IDatabase;

namespace Roki.Modules.Rsvp.Services
{
    public class RsvpService : IRokiService
    {
        private const string Error = "RSVP Event setup timed out. No message received.\nIf you still want to setup and event, you must start over.";
        private const string Stop = "RSVP Event setup canceled.\nIf you still want to setup and event, you must start over.";
        private const string ErrorEdit = "No message received. RSVP Event edit cancelled. All changes made are lost.";
        private const string StopEdit = "RSVP Event edit stopped.";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly IEmote Confirm = new Emoji("✅");
        private static readonly IEmote Cancel = new Emoji("❌");
        private static readonly IEmote Uncertain = new Emoji("❔");

        private static readonly IEmote[] Choices =
        {
            Confirm,
            Cancel,
            Uncertain
        };

        private readonly ConcurrentDictionary<int, Event> _activeReminders = new();
        private readonly DiscordSocketClient _client;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfigurationService _config;
        private readonly IDatabase _cache;
        private Timer _timer;

        public RsvpService(DiscordSocketClient client, IServiceScopeFactory scopeFactory, IConfigurationService config, IRedisCache cache)
        {
            _client = client;
            _scopeFactory = scopeFactory;
            _config = config;
            _cache = cache.Redis.GetDatabase();
            _client.ReactionAdded += ReactionHandler;
            EventTimer();
        }

        private void EventTimer()
        {
            _timer = new Timer(EventUpdate, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
        }

        private async void EventUpdate(object state)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
            try
            {
                DateTime now = DateTime.UtcNow.Date;
                foreach (Event e in context.Events.AsNoTracking().Where(x => x.StartDate >= now))
                {
                    var message = await _client.GetGuild(e.GuildId).GetTextChannel(e.ChannelId).GetMessageAsync(e.MessageId).ConfigureAwait(false) as IUserMessage;
                    IEmbed old = message?.Embeds.First();
                    if (old == null)
                    {
                        _activeReminders.TryRemove(e.Id, out _);
                        await _cache.KeyDeleteAsync($"event:{e.MessageId}");
                        continue;
                    }

                    await _cache.StringSetAsync($"event:{e.MessageId}", e.Id, when: When.NotExists);
                    
                    EmbedField part = old.Fields.First(f => f.Name.StartsWith("P", StringComparison.Ordinal));
                    EmbedField und = old.Fields.First(f => f.Name.StartsWith("U", StringComparison.Ordinal));
                    EmbedBuilder newEmbed = new EmbedBuilder().WithDynamicColor(e.GuildId)
                        .WithAuthor(old.Author?.Name, old.Author?.IconUrl)
                        .WithTitle($"`{e.Id}` {e.Name}")
                        .AddField("Description", e.Description)
                        .AddField("Event Date", $"```{e.StartDate:f} UTC```See footer for local time.")
                        .AddField(part.Name, part.Value)
                        .AddField(und.Name, und.Value)
                        .WithTimestamp(e.StartDate)
                        .WithDescription($"Starts in```{(e.StartDate - now).ToReadableString()}```")
                        .WithFooter("Event starts");
                    if (e.StartDate.AddMinutes(-45) <= now && !_activeReminders.ContainsKey(e.Id))
                    {
                        _activeReminders.TryAdd(e.Id, e);
                        Logger.Info("Event #{id} reminder countdown has started", e.Id);
                        await StartEventCountdowns(e).ConfigureAwait(false);
                    }

                    if (e.StartDate <= now)
                    {
                        newEmbed.WithDescription("Event started")
                            .WithFooter("Event started");
                        await message.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
                        _activeReminders.Remove(e.Id, out _);
                        await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                        await _cache.KeyDeleteAsync($"event:{e.MessageId}");
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
            IUserMessage q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                .WithTitle("RSVP Event Setup - Step 1")
                .WithDescription(
                    $"Which **channel** should the event be in?\n`this`, `current` or `here` for current channel, `default` for default channel\n(default is <#{ctx.Guild.DefaultChannelId}>)") // todo save in db
                .WithFooter("Type stop to cancel event setup")
            ).ConfigureAwait(false);
            toDelete.Add(q1);
            SocketMessage replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
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
            {
                eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
            }
            else if (replyMessage.Content.Contains("this", StringComparison.OrdinalIgnoreCase) ||
                     replyMessage.Content.Contains("current", StringComparison.OrdinalIgnoreCase) ||
                     replyMessage.Content.Contains("here", StringComparison.OrdinalIgnoreCase))
            {
                eventChannel = ctx.Channel;
            }
            else if (replyMessage.Content.Contains("default", StringComparison.OrdinalIgnoreCase))
            {
                eventChannel = _client.GetChannel(ctx.Guild.DefaultChannelId) as IMessageChannel; // todo save in db
            }

            while (eventChannel == null)
            {
                IUserMessage err = await ctx.Channel.SendErrorAsync("No channel specified. Please specify the channel using #\ni.e. <#217668245644247041>").ConfigureAwait(false);
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
                {
                    eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
                }
                else if (replyMessage.Content.Contains("this", StringComparison.OrdinalIgnoreCase) ||
                         replyMessage.Content.Contains("current", StringComparison.OrdinalIgnoreCase) ||
                         replyMessage.Content.Contains("here", StringComparison.OrdinalIgnoreCase))
                {
                    eventChannel = ctx.Channel;
                }
                else if (replyMessage.Content.Contains("default", StringComparison.OrdinalIgnoreCase))
                {
                    eventChannel = _client.GetChannel(ctx.Guild.DefaultChannelId) as IMessageChannel; // todo save in db
                }
            }

            IUserMessage q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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

            string eventTitle = replyMessage.Content.TrimTo(250, true);

            IUserMessage q3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithTitle("RSVP Event Setup - Step 3")
                    .WithDescription($"The title of the event has been set to: {Format.Bold(eventTitle)}\n" +
                                     "Please enter a **description** for the event.\n")
                    .WithFooter("Type stop to cancel event setup"))
                .ConfigureAwait(false);
            toDelete.Add(q3);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }

            string eventDesc = replyMessage.Content.TrimTo(1000, true);
            toDelete.Add(replyMessage as IUserMessage);
            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                return;
            }

            IUserMessage q4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
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

            DateTimeOffset? eventDate = ParseDateTimeFromString(replyMessage.Content);
            toDelete.Add(replyMessage as IUserMessage);
            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel.SendErrorAsync(Stop).ConfigureAwait(false);
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                return;
            }

            while (eventDate == null || eventDate.Value <= DateTimeOffset.Now)
            {
                IUserMessage err = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`")
                    .ConfigureAwait(false);
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
            {
                dateStr += "UTC";
            }

            EmbedBuilder q4Confirm = new EmbedBuilder().WithDynamicColor(ctx)
                .WithTitle("RSVP Event Setup - Step 4")
                .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`")
                .WithFooter("Type stop to cancel event setup");
            IUserMessage conf = await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
            toDelete.Add(conf);
            SocketMessage confirm = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
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
                IUserMessage err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithTitle("RSVP Event Setup - Step 4")
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
                    IUserMessage err2 = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`")
                        .ConfigureAwait(false);
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
                {
                    dateStr += "UTC";
                }

                q4Confirm = new EmbedBuilder().WithDynamicColor(ctx)
                    .WithTitle("RSVP Event Setup - Step 4")
                    .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`")
                    .WithFooter("Type stop to cancel event setup");
                IUserMessage con = await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
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

            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
            await using IDbContextTransaction trans = await context.Database.BeginTransactionAsync();
            try
            {
                var ev = new Event(ctx.User.Id, ctx.Guild.Id, ctx.Channel.Id, eventTitle, eventDesc, eventDate.Value.UtcDateTime);
                await context.Events.AddAsync(ev);
                await context.SaveChangesAsync();
                TimeSpan startsIn = eventDate.Value - DateTimeOffset.Now;
                EmbedBuilder eventEmbed = new EmbedBuilder().WithDynamicColor(ctx)
                    .WithTitle($"`{ev.Id}` {eventTitle}")
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
                await _cache.StringSetAsync($"event:{rsvp.Id}", ev.Id, flags: CommandFlags.FireAndForget);
                await context.SaveChangesAsync();
                await trans.CommitAsync();
                await rsvp.AddReactionsAsync(Choices).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await ctx.Channel.SendErrorAsync("Something went wrong when trying to create event");
                Logger.Error(e, "Something went wrong when trying to create event");
            }
        }

        public async Task EditEvent(ICommandContext ctx)
        {
            var toDelete = new List<IUserMessage> {ctx.Message};
            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
            await using IDbContextTransaction trans = await context.Database.BeginTransactionAsync();
            try
            {
                List<Event> events = await context.Events.AsQueryable()
                    .Where(x => x.HostId == ctx.User.Id)
                    // soft limit
                    .Take(15)
                    .ToListAsync();

                if (events.Count == 0)
                {
                    await ctx.Channel.SendErrorAsync("You do not have any active events.").ConfigureAwait(false);
                    return;
                }

                SocketMessage replyMessage;
                Event activeEditEvent = events.First();
                if (events.Count > 1)
                {
                    IUserMessage q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithTitle("RSVP Event Editor")
                        .WithDescription("Which Event would you like to edit? Type in the # to select.\n" +
                                         $"{string.Join("\n", events.Select(e => $"`{e.Id}`: {e.Name} `{e.StartDate:f}`"))}")
                        .WithFooter("Type stop to cancel event edit")
                    ).ConfigureAwait(false);
                    toDelete.Add(q1);
                    replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                    if (replyMessage == null)
                    {
                        goto NoResponse;
                    }

                    toDelete.Add(replyMessage as IUserMessage);
                    if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        goto StopEdit;
                    }

                    activeEditEvent = events.FirstOrDefault(e => e.Id.ToString() == replyMessage.Content);
                    while (activeEditEvent == null)
                    {
                        IUserMessage err = await ctx.Channel.SendErrorAsync("Cannot find an event with that ID, please try again.").ConfigureAwait(false);
                        toDelete.Add(err);
                        replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                        if (replyMessage == null)
                        {
                            goto NoResponse;
                        }

                        toDelete.Add(replyMessage as IUserMessage);
                        if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            goto StopEdit;
                        }

                        activeEditEvent = events.FirstOrDefault(e => e.Id.ToString() == replyMessage.Content);
                    }
                }

                IUserMessage q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithTitle("RSVP Event Editor")
                    .WithDescription(
                        $"What do you want to change for: `{activeEditEvent.Id}` **{activeEditEvent.Name}**\n`1.` Edit Title\n`2.` Edit Description\n`3.` Edit Event Date\n`4.` Delete Event")
                    .WithFooter("Type stop to finish editing")
                ).ConfigureAwait(false);
                toDelete.Add(q2);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    goto NoResponse;
                }

                toDelete.Add(replyMessage as IUserMessage);
                while (!replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    // Edit Title
                    SocketMessage editReply;
                    if (replyMessage.Content.StartsWith("1", StringComparison.OrdinalIgnoreCase))
                    {
                        IUserMessage e1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Edit Title")
                                .WithDescription("What should the new **title** be?"))
                            .ConfigureAwait(false);
                        toDelete.Add(e1);
                        editReply = await ReplyHandler(ctx).ConfigureAwait(false);
                        if (editReply == null)
                        {
                            goto NoResponse;
                        }

                        toDelete.Add(editReply as IUserMessage);
                        if (editReply.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            goto StopEdit;
                        }

                        activeEditEvent.Name = editReply.Content.TrimTo(250, true);
                        await context.SaveChangesAsync();

                        IUserMessage er1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Edit Title")
                                .WithDescription($"The event title has been set to: **{activeEditEvent.Name}**"))
                            .ConfigureAwait(false);
                        toDelete.Add(er1);
                    }
                    // Edit Description
                    else if (replyMessage.Content.StartsWith("2", StringComparison.OrdinalIgnoreCase))
                    {
                        IUserMessage e2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Edit Description")
                                .WithDescription("What should the new **description** be?"))
                            .ConfigureAwait(false);
                        toDelete.Add(e2);
                        editReply = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                        if (editReply == null)
                        {
                            goto NoResponse;
                        }

                        toDelete.Add(editReply as IUserMessage);
                        if (editReply.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            goto StopEdit;
                        }

                        activeEditEvent.Description = editReply.Content.TrimTo(1000, true);
                        await context.SaveChangesAsync();

                        IUserMessage er2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Edit Description")
                                .WithDescription($"The event description has been changed to:\n{activeEditEvent.Description}"))
                            .ConfigureAwait(false);
                        toDelete.Add(er2);
                    }
                    // Edit Event Date
                    else if (replyMessage.Content.StartsWith("3", StringComparison.OrdinalIgnoreCase))
                    {
                        IUserMessage e3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Edit Event Date")
                                .WithDescription("What should the new event date be?" +
                                                 $"Default timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`" +
                                                 "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
                            .ConfigureAwait(false);
                        toDelete.Add(e3);
                        editReply = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                        if (editReply == null)
                        {
                            goto NoResponse;
                        }

                        toDelete.Add(editReply as IUserMessage);
                        if (editReply.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            goto StopEdit;
                        }

                        DateTimeOffset? newDate = ParseDateTimeFromString(editReply.Content);
                        while (newDate == null || newDate.Value <= DateTimeOffset.Now)
                        {
                            IUserMessage err = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`")
                                .ConfigureAwait(false);
                            toDelete.Add(err);
                            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                            if (replyMessage == null)
                            {
                                goto NoResponse;
                            }

                            newDate = ParseDateTimeFromString(replyMessage.Content);
                            toDelete.Add(replyMessage as IUserMessage);
                            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                            {
                                goto StopEdit;
                            }
                        }

                        var dateStr = newDate.Value.ToString("f");
                        if (newDate.Value.Offset == TimeSpan.Zero)
                        {
                            dateStr += "UTC";
                        }

                        IUserMessage econ3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Edit Event Date")
                                .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`"))
                            .ConfigureAwait(false);
                        toDelete.Add(econ3);
                        SocketMessage con = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                        if (con == null)
                        {
                            goto NoResponse;
                        }

                        toDelete.Add(con as IUserMessage);
                        if (con.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                        {
                            goto StopEdit;
                        }

                        while (!con.Content.Contains("yes", StringComparison.OrdinalIgnoreCase) || !con.Content.Contains("y", StringComparison.OrdinalIgnoreCase))
                        {
                            IUserMessage err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithTitle("RSVP Event Editor - Edit Event Date")
                                    .WithDescription(
                                        $"Please enter the correct date. Default timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`\n" +
                                        "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
                                .ConfigureAwait(false);
                            toDelete.Add(err);
                            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                            if (replyMessage == null)
                            {
                                goto NoResponse;
                            }

                            newDate = ParseDateTimeFromString(replyMessage.Content);
                            toDelete.Add(replyMessage as IUserMessage);
                            if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                            {
                                goto StopEdit;
                            }

                            while (newDate == null || newDate.Value <= DateTimeOffset.Now)
                            {
                                IUserMessage err2 = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`")
                                    .ConfigureAwait(false);
                                toDelete.Add(err2);
                                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                                if (replyMessage == null)
                                {
                                    goto NoResponse;
                                }

                                newDate = ParseDateTimeFromString(replyMessage.Content);
                                toDelete.Add(replyMessage as IUserMessage);
                                if (replyMessage.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                                {
                                    goto StopEdit;
                                }
                            }

                            dateStr = newDate.Value.ToString("f");
                            if (newDate.Value.Offset == TimeSpan.Zero)
                            {
                                dateStr += "UTC";
                            }

                            IUserMessage con2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                    .WithTitle("RSVP Event Editor - Edit Event Date")
                                    .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`"))
                                .ConfigureAwait(false);
                            toDelete.Add(con2);
                            con = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                            if (con == null)
                            {
                                goto NoResponse;
                            }

                            toDelete.Add(con as IUserMessage);
                            if (con.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                            {
                                goto StopEdit;
                            }
                        }

                        activeEditEvent.StartDate = newDate.Value.UtcDateTime;
                        await context.SaveChangesAsync();

                        IUserMessage er2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Edit Event Date")
                                .WithDescription($"The event date has been changed to:\n`{activeEditEvent.StartDate:f}` UTC"))
                            .ConfigureAwait(false);
                        toDelete.Add(er2);
                    }
                    // Delete
                    else if (replyMessage.Content.StartsWith("4", StringComparison.OrdinalIgnoreCase))
                    {
                        IUserMessage e4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                                .WithTitle("RSVP Event Editor - Delete Event")
                                .WithDescription($"Are you sure you want to delete the event? `yes`/`no`\n`{activeEditEvent.Id}` **{activeEditEvent.Name}**\n**You cannot undo this process.**"))
                            .ConfigureAwait(false);
                        toDelete.Add(e4);
                        replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                        if (replyMessage == null)
                        {
                            goto NoResponse;
                        }

                        if (replyMessage.Content.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                            replyMessage.Content.Equals("y", StringComparison.OrdinalIgnoreCase))
                        {
                            var message = await _client.GetGuild(activeEditEvent.GuildId).GetTextChannel(activeEditEvent.ChannelId).GetMessageAsync(activeEditEvent.MessageId) as IUserMessage; 
                            context.Events.Remove(activeEditEvent);
                            await context.SaveChangesAsync();
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
                                    .WithDescription($"`{activeEditEvent.Id}` **{activeEditEvent.Name}** has been deleted"))
                                .ConfigureAwait(false);
                            await _cache.KeyDeleteAsync($"event:{message.Id}");
                            await context.SaveChangesAsync();
                            await trans.CommitAsync();
                            return;
                        }
                    }
                    else
                    {
                        IUserMessage err = await ctx.Channel.SendErrorAsync("Unknown Option, please select a valid option.").ConfigureAwait(false);
                        toDelete.Add(err);
                    }

                    IUserMessage q2Repeat = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithTitle("RSVP Event Editor")
                        .WithDescription(
                            $"What else do you want to change for: `{activeEditEvent.Id}` **{activeEditEvent.Name}**\n1. Edit Title\n2. Edit Description\n3. Edit Start Date\n4. Delete Event")
                        .WithFooter("Type stop to finish editing")
                    ).ConfigureAwait(false);
                    toDelete.Add(q2Repeat);
                    replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    if (replyMessage == null)
                    {
                        goto NoResponse;
                    }
                }

                StopEdit:

                await ctx.Channel.SendErrorAsync(StopEdit).ConfigureAwait(false);
                await context.SaveChangesAsync();
                await trans.CommitAsync();
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithTitle("RSVP Event Editor")
                        .WithDescription("The event editor has been stopped. All changes saved."))
                    .ConfigureAwait(false);
                return;

                NoResponse:

                await ctx.Channel.SendErrorAsync(ErrorEdit).ConfigureAwait(false);
                await trans.RollbackAsync();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Something went wrong when trying to edit Event");
                await trans.RollbackAsync();
            }
            finally
            {
                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
            }
        }

        private async Task<SocketMessage> ReplyHandler(ICommandContext ctx, TimeSpan? timeout = null)
        {
            string prefix = await _config.GetGuildPrefix(ctx.Guild.Id);

            timeout ??= TimeSpan.FromMinutes(5);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != ctx.Channel.Id || message.Author.Id != ctx.User.Id)
                {
                    return Task.CompletedTask;
                }

                if (message.Content.StartsWith(prefix)) // ignore commands
                {
                    return Task.CompletedTask;
                }

                eventTrigger.SetResult(message);
                return Task.CompletedTask;
            }

            _client.MessageReceived += Handler;

            Task<SocketMessage> trigger = eventTrigger.Task;
            Task<bool> cancel = cancelTrigger.Task;
            Task delay = Task.Delay(timeout.Value);
            Task task = await Task.WhenAny(trigger, cancel, delay).ConfigureAwait(false);

            _client.MessageReceived -= Handler;

            if (task == trigger)
            {
                return await trigger.ConfigureAwait(false);
            }

            return null;
        }

        private async Task ReactionHandler(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channel, SocketReaction r)
        {
            if (r.User.Value.IsBot || !Choices.Contains(r.Emote) || await _cache.KeyExistsAsync($"event:{cache.Id}"))
            {
                return;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

            var key = (int) await _cache.StringGetAsync($"event:{cache.Id}");
            Event ev = await context.Events.AsQueryable().SingleAsync(x => x.Id == key);

            IUserMessage msg;
            IEmbed old;
            try
            {
                msg = await cache.DownloadAsync().ConfigureAwait(false);
                old = msg.Embeds.First();
                if (old == null)
                {
                    return;
                }
            }
            catch (Exception)
            {
                _activeReminders.TryRemove((int) await _cache.StringGetAsync($"event:{cache.Id}"), out _);
                await _cache.KeyDeleteAsync($"event:{cache.Id}");
                return;
            }

            EmbedBuilder newEmbed = new EmbedBuilder().WithDynamicColor(((IGuildChannel) channel).GuildId)
                .WithAuthor(old.Author?.Name, old.Author?.IconUrl)
                .WithTitle(ev.Name)
                .AddField("Description", ev.Description)
                .AddField("Event Date", $"```{ev.StartDate:f} UTC```See footer for local time.")
                .WithTimestamp(ev.StartDate)
                .WithDescription($"Starts in```{(ev.StartDate - DateTime.UtcNow).ToReadableString()}```")
                .WithFooter("Event starts");

            IUser user = r.User.Value;
            var username = user.ToString();
            List<string> und = ev.Undecided;
            List<string> par = ev.Participants;

            if (r.Emote.Equals(Confirm))
            {
                await msg.RemoveReactionAsync(Cancel, user).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Uncertain, user).ConfigureAwait(false);
                if (par.Contains(username))
                {
                    return;
                }

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
                {
                    und.Remove(username);
                }

                if (par.Contains(username))
                {
                    par.Remove(username);
                }
            }
            else
            {
                await msg.RemoveReactionAsync(Cancel, user).ConfigureAwait(false);
                await msg.RemoveReactionAsync(Confirm, user).ConfigureAwait(false);
                if (und.Contains(username))
                {
                    return;
                }

                if (par.Contains(username))
                {
                    par.Remove(username);
                }

                und.Add(username);
            }

            ev.Participants = par;
            ev.Undecided = und;

            if (par.Count == 0)
            {
                newEmbed.AddField($"Participants ({par.Count})", "```None```");
            }
            else
            {
                newEmbed.AddField($"Participants ({par.Count})", $"```{string.Join("\n", par)}```");
            }

            if (und.Count == 0)
            {
                newEmbed.AddField("Undecided", "```None```");
            }
            else
            {
                newEmbed.AddField($"Undecided ({und.Count})", $"```{string.Join("\n", und)}```");
            }

            await msg.ModifyAsync(m => m.Embed = newEmbed.Build()).ConfigureAwait(false);
        }

        private static DateTimeOffset? ParseDateTimeFromString(string datetime)
        {
            if (datetime.Contains("tomorrow", StringComparison.OrdinalIgnoreCase))
            {
                DateTimeOffset tmr = DateTimeOffset.Now.AddDays(1);
                datetime = datetime.Replace("tomorrow", tmr.ToString("d"), StringComparison.OrdinalIgnoreCase);
            }
            else if (datetime.Contains("tmr", StringComparison.OrdinalIgnoreCase))
            {
                DateTimeOffset tmr = DateTimeOffset.Now.AddDays(1);
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
            TimeSpan thirtyMinutes = e.StartDate.AddMinutes(-30) - DateTime.UtcNow;
            TimeSpan startTime = e.StartDate - DateTime.UtcNow;

            if (thirtyMinutes >= TimeSpan.Zero)
            {
                Logger.Info("Event {name}: sending 30 minute reminder in {thirtymin}", e.Id, startTime);
                SendNotification(e.Id, thirtyMinutes, NotificationType.ThirtyMinutes);
            }

            if (startTime >= TimeSpan.Zero)
            {
                Logger.Info("Event {name}: sending start reminder in {starttime}", e.Id, startTime);
                SendNotification(e.Id, startTime, NotificationType.Starting);
            }

            return Task.CompletedTask;
        }

        private void SendNotification(int id, TimeSpan delay, NotificationType type)
        {
            Task _ = Task.Run(async () =>
            {
                await Task.Delay(delay).ConfigureAwait(false);

                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                Event latestEvent = await context.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);

                if (latestEvent.Participants.Count == 0)
                {
                    return;
                }

                List<string> participants = latestEvent.Participants;
                foreach (string participant in participants)
                {
                    try
                    {
                        string[] username = participant.Split('#');
                        SocketUser user = _client.GetUser(username[0], username[1]);
                        SocketGuild guild = _client.GetGuild(latestEvent.GuildId);
                        IDMChannel dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        EmbedBuilder embed = new EmbedBuilder().WithOkColor().WithFooter($"From server: {guild.Name}");

                        if (type == NotificationType.Starting)
                        {
                            embed.WithTitle($"`#{latestEvent.Id}` {latestEvent.Name} starting now!")
                                .WithDescription($"The event you registered for: {latestEvent.Name} is starting now!");
                        }
                        else
                        {
                            embed.WithTitle($"`#{latestEvent.Id}` {latestEvent.Name} reminder")
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
            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
            return await context.Events.AsNoTracking().Where(x => x.GuildId == guildId && x.StartDate >= DateTime.UtcNow).ToListAsync();
        }

        private enum NotificationType
        {
            ThirtyMinutes,
            Starting
        }
    }
}