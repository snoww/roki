using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Services.Database.Models
{
    public class Message
    {
        public Message(ulong id, ulong channelId, ulong guildId, ulong authorId, string content, ulong? repliedTo = null, List<string> attachments = null)
        {
            Id = id;
            ChannelId = channelId;
            GuildId = guildId;
            AuthorId = authorId;
            Content = content;
            RepliedTo = repliedTo;
            Attachments = attachments;
        }

        public ulong Id { get; }
        public ulong ChannelId { get; }
        public ulong GuildId { get; }
        public ulong AuthorId { get; }
        public string Content { get; }
        public ulong? RepliedTo { get; }
        public List<Edit> Edits { get; set; }
        public List<string> Attachments { get; }
        public bool Deleted { get; set; }
    }

    public class Edit
    {
        [JsonPropertyName("content")]
        public string Content { get; init; }
        [JsonPropertyName("attachments")]
        public List<string> Attachments { get; init; }
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; }

        public Edit(string content, List<string> attachments, DateTime timestamp)
        {
            Content = content;
            Attachments = attachments;
            Timestamp = timestamp;
        }
    }
}
