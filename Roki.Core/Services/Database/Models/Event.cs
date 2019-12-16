using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("events")]
    public class Event : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ulong Host { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public string Participants { get; set; }
        // why not List<string> ???
//        public ulong[] WaitingList { get; set; } = {};
        public string Undecided { get; set; }
    }
}