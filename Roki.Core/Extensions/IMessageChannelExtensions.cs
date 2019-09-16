using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Roki.Extensions
{
    public static class IMessageChannelExtensions
    {
        private static readonly IEmote arrow_left = new Emoji("⬅");
        private static readonly IEmote arrow_right = new Emoji("➡");

        public static Task<IUserMessage> EmbedAsync(this IMessageChannel channel, EmbedBuilder embed, string msg = "")
        {
            return channel.SendMessageAsync(msg, embed: embed.Build(),
                options: new RequestOptions {RetryMode = RetryMode.AlwaysRetry});
        }

        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string title, string text, string url = null, string footer = null)
        {
            var eb = new EmbedBuilder().WithOkColor().WithDescription(text)
                .WithTitle(title);
            if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                eb.WithUrl(url);
            if (!string.IsNullOrWhiteSpace(footer))
                eb.WithFooter(efb => efb.WithText(footer));
            return ch.SendMessageAsync("", embed: eb.Build());
        }

        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string text)
        {
            return ch.SendMessageAsync("", embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build());
        }

        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string error)
        {
            return ch.SendMessageAsync("", embed: new EmbedBuilder().WithErrorColor().WithDescription(error).Build());
        }

        public static Task SendPaginatedConfirmAsync(this ICommandContext ctx,
            int currentPage, Func<int, EmbedBuilder> pageFunc, int totalElements,
            int itemsPerPage, bool addPaginatedFooter = true)
        {
            return ctx.SendPaginatedConfirmAsync(currentPage,
                x => Task.FromResult(pageFunc(x)), totalElements, itemsPerPage, addPaginatedFooter);
        }

        private static async Task SendPaginatedConfirmAsync(this ICommandContext ctx, int currentPage,
            Func<int, Task<EmbedBuilder>> pageFunc, int totalElements, int itemsPerPage, bool addPaginatedFooter = true)
        {
            var embed = await pageFunc(currentPage).ConfigureAwait(false);

            var lastPage = (totalElements - 1) / itemsPerPage;

            if (addPaginatedFooter)
                embed.AddPaginatedFooter(currentPage, lastPage);

            var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);

            if (lastPage == 0)
                return;

            await msg.AddReactionAsync(arrow_left).ConfigureAwait(false);
            await msg.AddReactionAsync(arrow_right).ConfigureAwait(false);

            await Task.Delay(2000).ConfigureAwait(false);

            var lastPageChange = DateTime.MinValue;

            async Task ChangePage(SocketReaction r)
            {
                try
                {
                    if (r.UserId != ctx.User.Id)
                        return;
                    if (DateTime.UtcNow - lastPageChange < TimeSpan.FromSeconds(1))
                        return;
                    if (r.Emote.Name == arrow_left.Name)
                    {
                        if (currentPage == 0)
                            return;
                        lastPageChange = DateTime.UtcNow;
                        var toSend = await pageFunc(--currentPage).ConfigureAwait(false);
                        if (addPaginatedFooter)
                            toSend.AddPaginatedFooter(currentPage, lastPage);
                        await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                    }
                    else if (r.Emote.Name == arrow_right.Name)
                    {
                        if (lastPage > currentPage)
                        {
                            lastPageChange = DateTime.UtcNow;
                            var toSend = await pageFunc(++currentPage).ConfigureAwait(false);
                            if (addPaginatedFooter)
                                toSend.AddPaginatedFooter(currentPage, lastPage);
                            await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception)
                {
                    //ignored
                }
            }

            using (msg.OnReaction((DiscordSocketClient) ctx.Client, ChangePage, ChangePage))
            {
                await Task.Delay(30000).ConfigureAwait(false);
            }

            try
            {
                if (msg.Channel is ITextChannel && ((SocketGuild) ctx.Guild).CurrentUser.GuildPermissions.ManageMessages)
                    await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
                else
                    await Task.WhenAll(msg.Reactions.Where(x => x.Value.IsMe)
                        .Select(x => msg.RemoveReactionAsync(x.Key, ctx.Client.CurrentUser)));
            }
            catch
            {
                // ignored
            }
        }
        
        public static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int? lastPage)
        {
            if (lastPage != null)
                return embed.WithFooter(efb => efb.WithText($"{curPage + 1} / {lastPage + 1}"));
            return embed.WithFooter(efb => efb.WithText(curPage.ToString()));
        }
    }
}