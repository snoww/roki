using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Roki.Extensions;
using Victoria;
using Victoria.Entities;

namespace Roki.Modules.Music.Extensions
{
    public static class MusicExtensions
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static TimeSpan TotalPlaytime(this IEnumerable<LavaTrack> queue)
        {
            return new TimeSpan(queue.Sum(t => t.Length.Ticks));
        }

        public static string PrettyTrack(this LavaTrack track)
        {
            return $"**[{track.Title.TrimTo(65)}]({track.Uri})**";
        }

        public static string PrettyFullTrack(this LavaTrack track)
        {
            return $"{track.PrettyTrack()}\n\t\t`{track.PrettyLength()} | {track.Provider.ToTitleCase()}`";
        }

        public static string PrettyFooter(this LavaTrack track, int volume)
        {
            return $"🔉 {volume}% | {track.PrettyLength()} | {track.Provider.ToTitleCase()}";
        }

        public static string PrettyLength(this LavaTrack track)
        {
            var time = track.Length.ToString(@"mm\:ss");
            var hrs = (int) track.Length.TotalHours;

            if (hrs > 0)
                return hrs + ":" + time;
            return time;
        }
    }
}