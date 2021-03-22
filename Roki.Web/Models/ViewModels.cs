using System.Collections.Generic;

namespace Roki.Web.Models
{
    public class ChannelViewModel
    {
        public Guild Guild { get; set; }
        public List<Channel> Channels { get; set; }
    }
}