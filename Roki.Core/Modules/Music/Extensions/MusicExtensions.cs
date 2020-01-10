using System;
using System.Collections.Generic;
using System.Linq;
using Roki.Extensions;
using Roki.Modules.Music.Common;
using Victoria;

namespace Roki.Modules.Music.Extensions
{
    public static class MusicExtensions
    {
        public static TimeSpan TotalPlaytime(this IEnumerable<RokiTrack> queue)
        {
            return new TimeSpan(queue.Sum(q => q.Duration.Ticks));
        }

        public static string PrettyTrack(this RokiTrack track)
        {
            return $"**[{track.Title.TrimTo(65)}]({track.Url})**";
        }

        public static string PrettyFullTrack(this RokiTrack track)
        {
            return $"{track.PrettyTrack()}\n\t\t`{track.PrettyLength()} | {track.User.Username}`";
        }

        public static string PrettyFooter(this RokiTrack track, int volume)
        {
            return $"🔉 {volume}% | {track.PrettyLength()} | {track.User.Username}";
        }

        public static string PrettyLength(this RokiTrack track)
        {
            var time = track.Duration.ToString(@"mm\:ss");
            var hrs = (int) track.Duration.TotalHours;

            if (hrs > 0)
                return hrs + ":" + time;
            return time;
        }
    }
}