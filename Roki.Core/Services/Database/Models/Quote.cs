using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Quote
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public ulong AuthorId { get; set; }
        public string Keyword { get; set; }
        public string Text { get; set; }
        public int UseCount { get; set; } = 1;

        public Quote(ulong guildId, ulong channelId, ulong messageId, ulong authorId, string keyword, string text)
        {
            GuildId = guildId;
            ChannelId = channelId;
            MessageId = messageId;
            AuthorId = authorId;
            Keyword = keyword;
            Text = text;
        }
    }
}
