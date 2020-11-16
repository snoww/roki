using Discord;
using Victoria;

namespace Roki.Modules.Music.Common
{
    public class RokiPlayer : LavaPlayer
    {
        public bool Autoplay { get; set; }
        public RokiPlayer(LavaSocket lavaSocket, IVoiceChannel voiceChannel, ITextChannel textChannel) : base(lavaSocket, voiceChannel, textChannel)
        {
        }
    }
}