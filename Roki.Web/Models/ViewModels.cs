using System.Collections.Generic;

namespace Roki.Web.Models
{
    public class GuildChannelModel
    {
        // public string Section { get; set; }
        public GuildConfig GuildConfig { get; set; }
        public List<Channel> Channels { get; set; }
    }
}