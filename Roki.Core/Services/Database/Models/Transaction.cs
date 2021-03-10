using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong Sender { get; set; }
        public ulong Recipient { get; set; }
        public long Amount { get; set; }
        public string Description { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        
        public Transaction(ulong sender, ulong recipient, ulong guildId, ulong channelId, ulong messageId, long amount, string description)
        {
            GuildId = guildId;
            Sender = sender;
            Recipient = recipient;
            Amount = amount;
            Description = description;
            ChannelId = channelId;
            MessageId = messageId;
        }
    }
}
