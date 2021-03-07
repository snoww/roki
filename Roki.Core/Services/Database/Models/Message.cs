using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Services.Database.Models
{
    public class Message
    {
        public ulong Id { get; set; }
        public ulong? ChannelId { get; set; }
        public ulong? GuildId { get; set; }
        public ulong AuthorId { get; set; }
        public string Content { get; set; }
        public ulong? RepliedTo { get; set; }
        public List<Edit> Edits { get; set; } = new();
        public List<string> Attachments { get; set; } = new();
        public bool Deleted { get; set; } = false;
    }

    public class Edit
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
        [JsonPropertyName("attachments")]
        public List<string> Attachments { get; set; } = new();
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
