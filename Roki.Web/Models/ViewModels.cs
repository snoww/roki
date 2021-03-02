using System.Collections.Generic;

namespace Roki.Web.Models
{
    public class GuildChannelModel
    {
        public string Section { get; set; }
        public ulong ChannelId { get; set; }
        public Guild Guild { get; set; }
        public List<ChannelSummary> Channels { get; set; }
        public Channel Channel { get; set; }
    }
}