using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Roki.Extensions
{
    public static class IDMChannelExtensions
    {
        private static readonly IEmote ArrowLeft = new Emoji("⬅");
        private static readonly IEmote ArrowRight = new Emoji("➡");
        
        public static Task SendPaginatedDmAsync(this IDMChannel dm, DiscordSocketClient client,
            int currentPage, Func<int, EmbedBuilder> pageFunc, int totalElements,
            int itemsPerPage)
        {
            return dm.SendPaginatedDmAsync(client, currentPage,
                x => Task.FromResult(pageFunc(x)), totalElements, itemsPerPage);
        }
        
        private static async Task SendPaginatedDmAsync(this IDMChannel dm, DiscordSocketClient client, int currentPage,
            Func<int, Task<EmbedBuilder>> pageFunc, int totalElements, int itemsPerPage)
        {
            var embed = await pageFunc(currentPage).ConfigureAwait(false);

            var lastPage = (totalElements - 1) / itemsPerPage;

            var msg = await dm.EmbedAsync(embed).ConfigureAwait(false);

            if (lastPage == 0)
                return;

            await msg.AddReactionAsync(ArrowLeft).ConfigureAwait(false);
            await msg.AddReactionAsync(ArrowRight).ConfigureAwait(false);

            await Task.Delay(2000).ConfigureAwait(false);

            var lastPageChange = DateTime.MinValue;

            async Task ChangePage(SocketReaction r)
            {
                try
                {
                    if (DateTime.UtcNow - lastPageChange < TimeSpan.FromSeconds(1))
                        return;
                    if (r.Emote.Name == ArrowLeft.Name)
                    {
                        if (currentPage == 0)
                            return;
                        lastPageChange = DateTime.UtcNow;
                        var toSend = await pageFunc(--currentPage).ConfigureAwait(false);
                        await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                    }
                    else if (r.Emote.Name == ArrowRight.Name)
                    {
                        if (lastPage > currentPage)
                        {
                            lastPageChange = DateTime.UtcNow;
                            var toSend = await pageFunc(++currentPage).ConfigureAwait(false);
                            await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                        }
                    }
                    await Task.Delay(500);
                    await msg.RemoveReactionAsync(r.Emote, r.User.Value).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    //ignored
                }
            }

            using (msg.OnReaction(client, ChangePage))
            {
                await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                await Task.WhenAll(msg.Reactions.Where(x => x.Value.IsMe)
                        .Select(x => msg.RemoveReactionAsync(x.Key, client.CurrentUser)));
            }
        }
    }
}