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
            var path = await Service.GetRandomNsfw(NsfwCategory.General).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Ass()
        {
            var path = await Service.GetRandomNsfw(NsfwCategory.Ass).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Boobs()
        {
            var path = await Service.GetRandomNsfw(NsfwCategory.Boobs).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Cosplay()
        {
            var path = await Service.GetRandomNsfw(NsfwCategory.Cosplay).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Hentai()
        {
            var path = await Service.GetRandomNsfw(NsfwCategory.Hentai).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Mild()
        {
            var path = await Service.GetRandomNsfw(NsfwCategory.Mild).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task NsfwGifs()
        {
            var path = await Service.GetRandomNsfw(NsfwCategory.Gifs).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Trans()
        {
            var path = await Service.GetRandomNsfw(NsfwCategory.Trans).ConfigureAwait(false);
            await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
        }
    }
}