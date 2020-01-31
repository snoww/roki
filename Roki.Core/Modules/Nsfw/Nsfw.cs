using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Modules.Nsfw.Services;

namespace Roki.Modules.Nsfw
{
    [RequireNsfw]
    public class Nsfw : RokiTopLevelModule<NsfwService>
    {
        [RokiCommand, Usage, Description, Aliases]
        public async Task NotSafeForWork()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.General).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Ass()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.Ass).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Boobs()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.Boobs).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Cosplay()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.Cosplay).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Hentai()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.Hentai).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Mild()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.Mild).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task NsfwGifs()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.Gifs).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Trans()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var url = await Service.GetRandomNsfw(NsfwCategory.Trans).ConfigureAwait(false);
            await Context.Channel.SendMessageAsync(url).ConfigureAwait(false);
        }
    }
}