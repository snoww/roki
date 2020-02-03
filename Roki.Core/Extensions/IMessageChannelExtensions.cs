using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Roki.Extensions
{
    public static class IMessageChannelExtensions
    {
        private static readonly IEmote ArrowLeft = new Emoji("⬅");
        private static readonly IEmote ArrowRight = new Emoji("➡");

        public static Task<IUserMessage> EmbedAsync(this IMessageChannel channel, EmbedBuilder embed, string msg = "")
        {
            return channel.SendMessageAsync(msg, embed: embed.Build(),
                options: new RequestOptions {RetryMode = RetryMode.AlwaysRetry});
        }
        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string error)
        {
            return ch.SendMessageAsync("", embed: new EmbedBuilder().WithErrorColor().WithDescription(error).Build());
        }

        public static Task SendPaginatedMessageAsync(this ICommandContext ctx,
            int currentPage, Func<int, EmbedBuilder> pageFunc, int totalElements,
            int itemsPerPage, bool addPaginatedFooter = true)
        {
            return ctx.SendPaginatedMessageAsync(currentPage,
                x => Task.FromResult(pageFunc(x)), totalElements, itemsPerPage, addPaginatedFooter);
        }

        private static async Task SendPaginatedMessageAsync(this ICommandContext ctx, int currentPage,
            Func<int, Task<EmbedBuilder>> pageFunc, int totalElements, int itemsPerPage, bool addPaginatedFooter = true)
        {
            var embed = await pageFunc(currentPage).ConfigureAwait(false);

            var lastPage = (totalElements - 1) / itemsPerPage;

            if (addPaginatedFooter)
                embed.AddPaginatedFooter(currentPage, lastPage);

            var message = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);

            if (lastPage == 0)
                return;

            await message.AddReactionAsync(ArrowLeft).ConfigureAwait(false);
            await message.AddReactionAsync(ArrowRight).ConfigureAwait(false);

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
                    if (r.Emote.Name == ArrowLeft.Name)
                    {
                        if (currentPage == 0)
                            return;
                        lastPageChange = DateTime.UtcNow;
                        var updatedEmbed = await pageFunc(--currentPage).ConfigureAwait(false);
                        if (addPaginatedFooter)
                            updatedEmbed.AddPaginatedFooter(currentPage, lastPage);
                        await message.ModifyAsync(x => x.Embed = updatedEmbed.Build()).ConfigureAwait(false);
                        await message.RemoveReactionAsync(ArrowLeft, ctx.User).ConfigureAwait(false);
                    }
                    else if (r.Emote.Name == ArrowRight.Name)
                    {
                        if (lastPage > currentPage)
                        {
                            lastPageChange = DateTime.UtcNow;
                            var updatedEmbed = await pageFunc(++currentPage).ConfigureAwait(false);
                            if (addPaginatedFooter)
                                updatedEmbed.AddPaginatedFooter(currentPage, lastPage);
                            await message.ModifyAsync(x => x.Embed = updatedEmbed.Build()).ConfigureAwait(false);
                            await message.RemoveReactionAsync(ArrowRight, ctx.User).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception)
                {
                    //ignored
                }
            }

            using (message.OnReaction((DiscordSocketClient) ctx.Client, ChangePage))
            {
                await Task.Delay(30000).ConfigureAwait(false);
            }

            try
            {
                if (message.Channel is ITextChannel && ((SocketGuild) ctx.Guild).CurrentUser.GuildPermissions.ManageMessages)
                    await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                else
                    await Task.WhenAll(message.Reactions.Where(x => x.Value.IsMe)
                        .Select(x => message.RemoveReactionAsync(x.Key, ctx.Client.CurrentUser)));
            }
            catch
            {
                // ignored
            }
        }

        private static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int lastPage)
        {
            return embed.WithFooter($"Page {curPage + 1} / {lastPage + 1}");
        }
    }
}