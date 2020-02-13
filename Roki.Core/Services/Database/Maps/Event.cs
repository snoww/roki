using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class Event
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ulong Host { get; set; }
        public DateTime StartDate { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public List<string> Participants { get; set; } = new List<string>();
//        public ulong[] WaitingList { get; set; } = {};
        public List<string> Undecided { get; set; } = new List<string>();
        public bool Deleted { get; set; } = false;
    }
}