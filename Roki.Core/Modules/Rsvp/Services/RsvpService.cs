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
        private static readonly IEmote Confirm = new Emoji("✅");
        private static readonly IEmote Cancel = new Emoji("❌");
        private static readonly IEmote Uncertain = new Emoji("❔");
        private const string Error = "RSVP Event setup timed out. No message received.\nIf you still want to setup and event, you must start over.";

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
                    .WithAuthor(old.Author?.Name, old.Author?.Url)
                    .WithTitle(old.Title)
                    .AddField("Description", e.Description)
                    .AddField("Event Date", $"`{e.StartDate::dddd, MMMM dd, yyyy h:mm tt}`")
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
        }

        public async Task CreateEvent(ICommandContext ctx)
        {
            var toDelete = new List<IUserMessage>();
            var q1 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup - Step 1")
//                    .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription("Which **channel** should the event be in?\n`this`, `current` or `here` for current channel, `default` for default channel\n(default is <#217668245644247041>)")
            ).ConfigureAwait(false);
            toDelete.Add(q1);
            var replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            toDelete.Add(replyMessage as IUserMessage);
            IMessageChannel eventChannel;
            if (replyMessage.Content.Contains("this", StringComparison.OrdinalIgnoreCase) || 
                replyMessage.Content.Contains("current", StringComparison.OrdinalIgnoreCase) || 
                replyMessage.Content.Contains("here", StringComparison.OrdinalIgnoreCase))
                eventChannel = ctx.Channel;
            else if (replyMessage.Content.Contains("default", StringComparison.OrdinalIgnoreCase))
                eventChannel = _client.GetChannel(220571291617329154) as IMessageChannel;
            else
                eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
            
            while (eventChannel == null)
            {
                var err = await ctx.Channel.SendErrorAsync("No channel specified. Please specify the channel using #\ni.e. <#220571291617329154>").ConfigureAwait(false);
                toDelete.Add(err);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(Error);
                    return;
                }
                toDelete.Add(replyMessage as IUserMessage);
                if (replyMessage.Content.Contains("this", StringComparison.OrdinalIgnoreCase) || 
                    replyMessage.Content.Contains("current", StringComparison.OrdinalIgnoreCase) || 
                    replyMessage.Content.Contains("here", StringComparison.OrdinalIgnoreCase))
                    eventChannel = ctx.Channel;
                else if (replyMessage.Content.Contains("default", StringComparison.OrdinalIgnoreCase))
                    eventChannel = _client.GetChannel(220571291617329154) as IMessageChannel;
                else
                    eventChannel = replyMessage.MentionedChannels.FirstOrDefault() as IMessageChannel;
            }

            var q2 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 2")
                    .WithDescription($"Using <#{eventChannel.Id}> for the event.\nNow enter a **title** for the event."))
                .ConfigureAwait(false);
            toDelete.Add(q2);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            toDelete.Add(replyMessage as IUserMessage);
            var eventTitle = replyMessage.Content.TrimTo(250, true);
            var q3 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 3")
                    .WithDescription($"The title of the event has been set to: {Format.Bold(eventTitle)}\n" +
                                     $"Please enter a **description** for the event.\n"))
                .ConfigureAwait(false);
            toDelete.Add(q3);
            replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (replyMessage == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            var eventDesc = replyMessage.Content;
            toDelete.Add(replyMessage as IUserMessage);
            var q4 = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 4")
                    .WithDescription($"Now please enter the date and time when this event starts.\nDefault timezone is `{TimeZoneInfo.Local.StandardName}` or you can specify `UTC`\n" +
                                     "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
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
            while (eventDate == null || eventDate.Value <= DateTimeOffset.Now)
            {
                var err = await ctx.Channel.SendErrorAsync("Could not parse the date. Please try again.\nExamples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`").ConfigureAwait(false);
                err.DeleteAfter(60);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(Error);
                    return;
                }
                eventDate = ParseDateTimeFromString(replyMessage.Content);
                toDelete.Add(replyMessage as IUserMessage);
            }
            
            var dateStr = eventDate.Value.ToString("f");
            if (eventDate.Value.Offset == TimeSpan.Zero)
                dateStr += "UTC";
            
            var q4Confirm = new EmbedBuilder().WithOkColor()
                .WithTitle("RSVP Event Setup - Step 4")
                .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`");
            
            var conf = await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
            toDelete.Add(conf);
            var confirm = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            if (confirm == null)
            {
                await ctx.Channel.SendErrorAsync(Error);
                return;
            }
            toDelete.Add(confirm as IUserMessage);
            while (!confirm.Content.Contains("yes", StringComparison.OrdinalIgnoreCase) || !confirm.Content.Contains("y", StringComparison.OrdinalIgnoreCase))
            {
                var err = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("RSVP Event Setup - Step 4")
                        .WithDescription($"Please enter the date this event starts. Default timezone is `{TimeZoneInfo.Local.StandardName}` or `UTC`" +
                                         "Examples: `tomorrow 1pm`, `5pm nov 24`, `21:30 jan 19 2020 utc`"))
                    .ConfigureAwait(false);
                err.DeleteAfter(60);
                replyMessage = await ReplyHandler(ctx, TimeSpan.FromMinutes(3)).ConfigureAwait(false);
                if (replyMessage == null)
                {
                    await ctx.Channel.SendErrorAsync(Error);
                    return;
                }

                eventDate = ParseDateTimeFromString(replyMessage.Content);
                toDelete.Add(replyMessage as IUserMessage);
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
                }
                dateStr = eventDate.Value.ToString("f");
                if (eventDate.Value.Offset == TimeSpan.Zero)
                    dateStr += "UTC";
                
                q4Confirm = new EmbedBuilder().WithOkColor()
                    .WithTitle("RSVP Event Setup - Step 4")
                    .WithDescription($"Is the date correct? `yes`/`no`\n`{dateStr}`");
                var con = await ctx.Channel.EmbedAsync(q4Confirm).ConfigureAwait(false);
                toDelete.Add(con);
                confirm = await ReplyHandler(ctx, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (confirm == null)
                {
                    await ctx.Channel.SendErrorAsync(Error);
                    return;
                }
                toDelete.Add(confirm as IUserMessage);
            }

            var startsIn = eventDate.Value - DateTimeOffset.Now;
            var eventEmbed = new EmbedBuilder().WithOkColor()
                .WithTitle(eventTitle)
                .WithAuthor(ctx.User.Username, ctx.User.GetAvatarUrl())
                .WithDescription($"Starts in `{startsIn.ToReadableString()}`")
                .AddField("Description", eventDesc)
                .AddField("Event Date", $"`{eventDate.Value:dddd, MMMM dd, yyyy h:mm tt}`")
                .AddField("Participants (0)", "```None```")
                .AddField("Undecided", "```None```")
                .WithFooter("Event starts")
                .WithTimestamp(eventDate.Value);

            await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
            var rsvp = await eventChannel.EmbedAsync(eventEmbed).ConfigureAwait(false);
            await rsvp.AddReactionsAsync(Choices).ConfigureAwait(false);
            
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

        public async Task ReactionHandler(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channel, SocketReaction r)
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
                .WithAuthor(old.Author?.Name, old.Author?.Url)
                .WithTitle(old.Title)
                .AddField("Description", ev.Description)
                .AddField("Event Date", date.Value)
                .WithTimestamp(old.Timestamp.GetValueOrDefault())
                .WithDescription($"Starts in `{(old.Timestamp.GetValueOrDefault() - DateTimeOffset.Now).ToReadableString()}`")
                .WithFooter("Event starts");

            var und = new List<string>();
            var par = new List<string>();
            if (!string.IsNullOrWhiteSpace(ev.Undecided))
                und = ev.Undecided.Split(',').ToList();
            if (!string.IsNullOrWhiteSpace(ev.Participants))
                par = ev.Participants.Split(',').ToList();

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

            ev.Participants = string.Join(',', par);
            ev.Undecided = string.Join(',', und);
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