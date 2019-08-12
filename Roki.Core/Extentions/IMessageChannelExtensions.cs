using System.Threading.Tasks;
using Discord;

namespace Roki.Extentions
{
    public static class IMessageChannelExtensions
    {
        public static Task<IUserMessage> EmbedAsync(this IMessageChannel channel, EmbedBuilder embed, string msg = "")
            => channel.SendMessageAsync(msg, embed: embed.Build(),
                options: new RequestOptions() { RetryMode  = RetryMode.AlwaysRetry });
        
        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string text)
            => ch.SendMessageAsync("", embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build());
    }
}