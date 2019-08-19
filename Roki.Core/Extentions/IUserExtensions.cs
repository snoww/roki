using Discord;
using System;

namespace Roki.Extensions
{
    public static class IUserExtensions
    {
        public static Uri RealAvatarUrl(this IUser usr, int size = 0)
        {
            var append = size <= 0
                ? ""
                : $"?size={size}";

            return usr.AvatarId == null
                ? null
                : new Uri(usr.AvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                    ? $"{DiscordConfig.CDNUrl}avatars/{usr.Id}/{usr.AvatarId}.gif" + append
                    : usr.GetAvatarUrl(ImageFormat.Auto) + append);
        }

    }
}
