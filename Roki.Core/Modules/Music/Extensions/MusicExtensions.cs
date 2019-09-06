using System;
using System.Linq;
using NLog;
using Victoria.Entities;
using Victoria.Queue;

namespace Roki.Modules.Music.Extensions
{
    public static class MusicExtensions
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static TimeSpan TotalPlaytime(this LavaTrack[] queue)
        {
            return new TimeSpan(queue.Sum(t => t.Length.Ticks));
        }
    }
}