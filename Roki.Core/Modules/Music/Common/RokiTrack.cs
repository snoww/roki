using System;
using Discord;
using Victoria;

namespace Roki.Modules.Music.Common
{
    public class RokiTrack : LavaTrack
    {
        public IGuildUser Queued { get; set; }
        public RokiTrack(LavaTrack lavaTrack) : base(lavaTrack)
        {
        }
    }
}