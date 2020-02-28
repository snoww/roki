using System;
using System.Collections.Generic;
using System.Linq;
using Roki.Extensions;
using Victoria;

namespace Roki.Modules.Music.Extensions
{
    public static class MusicExtensions
    {
        public static TimeSpan TotalPlaytime(this IEnumerable<LavaTrack> queue)
        {
            return new TimeSpan(queue.Sum(q => q.Duration.Ticks));
        }

        public static string PrettyTrack(this LavaTrack track)
        {
            return $"**[{track.Title.TrimTo(65)}]({track.Url})**";
        }

        public static string PrettyFullTrack(this LavaTrack track)
        {
            return track.Queued != null ? $"{track.PrettyTrack()}\n\t\t`{track.PrettyLength()} | {track.Queued.Username}`"
                : $"{track.PrettyTrack()}\n\t\t`{track.PrettyLength()} | Autoplay";
        }

        public static string PrettyFooter(this LavaTrack track, int volume)
        {
            return track.Queued != null ? $"🔉 {volume}% | {track.PrettyLength()} | {track.Queued.Username}"
                : $"🔉 {volume}% | {track.PrettyLength()} | Autoplay";
        }

        public static string PrettyLength(this LavaTrack track)
        {
            var time = track.Duration.ToString(@"mm\:ss");
            var hrs = track.Duration.TotalHours;

            if (hrs > 0)
                return hrs + ":" + time;
            return time;
        }
    }
}