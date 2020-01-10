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
        public static TimeSpan TotalPlaytime(this IEnumerable<LavaTrack> queue)
        {
            return new TimeSpan(queue.Sum(q => q.Duration.Ticks));
        }

        public static string PrettyTrack(this RokiRequest request)
        {
            return $"**[{request.Track.Title.TrimTo(65)}]({request.Track.Url})**";
        }

        public static string PrettyFullTrack(this RokiRequest request)
        {
            return $"{request.PrettyTrack()}\n\t\t`{request.PrettyLength()} | {request.User.Username}`";
        }

        public static string PrettyFooter(this RokiRequest request, int volume)
        {
            return $"🔉 {volume}% | {request.PrettyLength()} | {request.User.Username}";
        }

        public static string PrettyLength(this RokiRequest request)
        {
            var time = request.Track.Duration.ToString(@"mm\:ss");
            var hrs = (int) request.Track.Duration.TotalHours;

            if (hrs > 0)
                return hrs + ":" + time;
            return time;
        }
    }
}