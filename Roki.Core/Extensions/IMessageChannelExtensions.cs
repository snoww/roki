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
        public static Task<IUserMessage> EmbedAsync(this IMessageChannel channel, EmbedBuilder embed, string msg = "")
            => channel.SendMessageAsync(msg, embed: embed.Build(),
                options: new RequestOptions() { RetryMode  = RetryMode.AlwaysRetry });
        
        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string text)
            => ch.SendMessageAsync("", embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build());
        
        private static readonly IEmote arrow_left = new Emoji("⬅");
        private static readonly IEmote arrow_right = new Emoji("➡");
        
        public static Task SendPaginatedConfirmAsync(this ICommandContext ctx, 
            int currentPage, Func<int, EmbedBuilder> pageFunc, int totalElements,
            int itemsPerPage, bool addPaginatedFooter = true)
            => ctx.SendPaginatedConfirmAsync(currentPage,
                x => Task.FromResult(pageFunc(x)), totalElements, itemsPerPage, addPaginatedFooter);
        
        private static async Task SendPaginatedConfirmAsync(this ICommandContext ctx, int currentPage, 
            Func<int, Task<EmbedBuilder>> pageFunc, int totalElements, int itemsPerPage, bool addPaginatedFooter = true)
        {
            var embed = await pageFunc(currentPage).ConfigureAwait(false);

            var lastPage = (totalElements - 1) / itemsPerPage;

            if (addPaginatedFooter)
                embed.AddPaginatedFooter(currentPage, lastPage);

            var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false) as IUserMessage;

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

            using (msg.OnReaction((DiscordSocketClient)ctx.Client, ChangePage, ChangePage))
            {
                await Task.Delay(30000).ConfigureAwait(false);
            }

            try
            {
                if(msg.Channel is ITextChannel && ((SocketGuild)ctx.Guild).CurrentUser.GuildPermissions.ManageMessages)
                {
                    await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
                }
                else
                {
                    await Task.WhenAll(msg.Reactions.Where(x => x.Value.IsMe)
                        .Select(x => msg.RemoveReactionAsync(x.Key, ctx.Client.CurrentUser)));
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}