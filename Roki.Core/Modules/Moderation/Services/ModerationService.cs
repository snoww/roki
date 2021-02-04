using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Moderation.Services
{
    public class ModerationService : IRokiService
    {
        public static async Task<bool> ValidPermissions(ICommandContext ctx, IGuildUser self, IGuildUser target, string task)
        {
            if (target.IsBot)
            {
                await ctx.Channel.SendErrorAsync($"Cannot {task} bots");
                return false;
            }

            if (target.Id == self.Id)
            {
                await ctx.Channel.SendErrorAsync($"Cannot {task} yourself");
                return false;
            }

            if (self!.GuildPermissions.RawValue <= target.GuildPermissions.RawValue)
            {
                await ctx.Channel.SendErrorAsync($"You do not have permission to {task} {target.Mention}");
                return false;
            }

            return true;
        }

        public static async Task<int> PruneMessagesAsync(ICommandContext ctx, IGuildUser user, int count)
        {
            IEnumerable<IMessage> messages = await ctx.Channel.GetMessagesAsync(count * 5).FlattenAsync();
            var toRemove = new List<IMessage>();
            foreach (IMessage message in messages)
            {
                if (message.Author.Id != user.Id)
                {
                    continue;
                }
                
                toRemove.Add(message);

                if (toRemove.Count >= count)
                {
                    break;
                }
            }
            await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(toRemove);

            return toRemove.Count;
        }
    }
}