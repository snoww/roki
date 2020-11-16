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

        public static string PrettyFullTrackWithCurrentPos(this RokiTrack track)
        {
            return track.Queued != null ? $"{track.PrettyTrack()}\n\t\t`{track.Position.PrettyLength()}/{track.Duration.PrettyLength()}` | `{track.Queued}`"
                : $"{track.PrettyTrack()}\n\t\t`{track.Position.PrettyLength()}/{track.Duration.PrettyLength()}` | `Autoplay`";
        }

        public static string PrettyFullTrack(this RokiTrack track)
        {
            return track.Queued != null ? $"{track.PrettyTrack()}\n\t\t`{track.Duration.PrettyLength()}` | `{track.Queued}`"
                : $"{track.PrettyTrack()}\n\t\t`{track.Duration.PrettyLength()}` | `Autoplay`";
        }

        public static string PrettyFooter(this RokiTrack track, int volume)
        {
            return track.Queued != null ? $"🔉 {volume}% | {track.Duration.PrettyLength()} | {track.Queued}"
                : $"🔉 {volume}% | {track.Duration.PrettyLength()} | Autoplay";
        }

        public static string PrettyLength(this TimeSpan timeSpan)
        {
            var time = timeSpan.ToString(@"mm\:ss");
            var hrs = timeSpan.TotalHours;

            return hrs >= 1 
                ? (int) hrs + ":" + time 
                : time;
        }
    }
}