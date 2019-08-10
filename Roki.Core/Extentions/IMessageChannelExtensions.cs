using System.Threading.Tasks;
using Discord;

namespace Roki.Extentions
{
    public static class IMessageChannelExtensions
    {
        public static Task<IUserMessage> EmbedAsync(this IMessageChannel channel, EmbedBuilder embed, string msg = "")
            => channel.SendMessageAsync(msg, embed: embed.Build(),
                options: new RequestOptions() { RetryMode  = RetryMode.AlwaysRetry });
    }
}