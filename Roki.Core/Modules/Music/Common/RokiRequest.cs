using Discord;
using Victoria;
using Victoria.Interfaces;

namespace Roki.Modules.Music.Common
{
    public class RokiRequest : IQueueable
    {
        public LavaTrack Track { get; set; }
        public IGuildUser User { get; set; }
    }
}