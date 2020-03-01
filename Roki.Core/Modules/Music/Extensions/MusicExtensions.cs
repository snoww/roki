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

        public static string PrettyFullTrackWithCurrentPos(this LavaTrack track)
        {
            return track.Queued != null ? $"{track.PrettyTrack()}\n\t\t`{track.Position.PrettyLength()}/{track.Duration.PrettyLength()}` | `{track.Queued}`"
                : $"{track.PrettyTrack()}\n\t\t`{track.Position.PrettyLength()}/{track.Duration.PrettyLength()}` | `Autoplay`";
        }

        public static string PrettyFullTrack(this LavaTrack track)
        {
            return track.Queued != null ? $"{track.PrettyTrack()}\n\t\t`{track.Duration.PrettyLength()}` | `{track.Queued}`"
                : $"{track.PrettyTrack()}\n\t\t`{track.Duration.PrettyLength()}` | `Autoplay`";
        }

        public static string PrettyFooter(this LavaTrack track, int volume)
        {
            return track.Queued != null ? $"🔉 {volume}% | {track.Duration.PrettyLength()} | {track.Queued}"
                : $"🔉 {volume}% | {track.Duration.PrettyLength()} | Autoplay";
        }

        private static string PrettyLength(this TimeSpan timeSpan)
        {
            var time = timeSpan.ToString(@"mm\:ss");
            var hrs = timeSpan.TotalHours;

            return hrs >= 1 
                ? (int) hrs + ":" + time 
                : time;
        }
    }
}