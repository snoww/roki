using System;
using System.Collections.Generic;
using Discord;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class Message
    {
        public ulong Id { get; set; }
        public ulong AuthorId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong GuildId { get; set; }
        public string Content { get; set; }
        [BsonIgnoreIfNull]
        public ulong? MessageReference { get; set; }
        [BsonIgnoreIfNull]
        public List<Edit> Edits { get; set; } = new List<Edit>();
        [BsonIgnoreIfNull]
        public List<string> Attachments { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class Edit
    {
        public string Content { get; set; }
        public List<string> Attachments { get; set; } = new List<string>();
        public DateTime EditedTimestamp { get; set; }
    }
}