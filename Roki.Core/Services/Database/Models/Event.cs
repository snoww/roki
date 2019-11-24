using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("events")]
    public class Event : DbEntity
    {
        [Column("name")]
        public string Name { get; set; }
        [Column("description")]
        public string Description { get; set; }
        [Column("host")]
        public ulong Host { get; set; }
        [Column("start_date")]
        public DateTimeOffset StartDate { get; set; }
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Column("channel_id")]
        public ulong ChannelId { get; set; }
        [Column("message_id")]
        public ulong MessageId { get; set; }
        [Column("participants")]
        public ulong[] Participants { get; set; } = {};
//        [Column("waiting_list")]
//        public ulong[] WaitingList { get; set; } = {};
        [Column("undecided")]
        public ulong[] Undecided { get; set; } = {};
        [Column("created_date")]
        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
    }
}