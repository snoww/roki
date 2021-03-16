using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Roki.Web.Models
{
    public class GuildChannelModel
    {
        // public string Section { get; set; }
        public GuildConfig GuildConfig { get; set; }
        public List<Channel> Channels { get; set; }
    }

    public class CoreSettingsModel
    {
        public string Prefix { get; set; }
        [JsonPropertyName("guild-logging")]
        public string Logging { get; set; }
        [JsonPropertyName("guild-curr")]
        public string Currency { get; set; }
        [JsonPropertyName("guild-xp")]
        public string Xp { get; set; }
        [JsonPropertyName("guild-show-help")]
        public string Help { get; set; }
    }
}