using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MongoDB.Driver;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Moderation.Services
{
    public class ModerationService : IRokiService
    {
        private readonly IMongoService _mongo;

        public ModerationService(IMongoService mongo)
        {
            _mongo = mongo;
        }

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
    }
}