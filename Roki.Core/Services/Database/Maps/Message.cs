using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class Message
    {
        [BsonId]
        public ulong MessageId { get; set; }
        public ulong AuthorId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong? GuildId { get; set; }
        public string Content { get; set; }
        public List<Edit> Edits { get; set; } = new List<Edit>();
        public List<string> Attachments { get; set; } = new List<string>();
        public DateTimeOffset Timestamp { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class Edit
    {
        public string Content { get; set; }
        public List<string> Attachments { get; set; } = new List<string>();
        public DateTimeOffset EditedTimestamp { get; set; }
    }
}