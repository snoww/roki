using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Quote
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong AuthorId { get; set; }
        public string Keyword { get; set; }
        public string Text { get; set; }
        public string Context { get; set; }
        public int UseCount { get; set; } = 1;
        public DateTime Date { get; set; } = DateTime.UtcNow;

        public Quote(ulong guildId, ulong authorId, string keyword, string text, string context)
        {
            GuildId = guildId;
            AuthorId = authorId;
            Keyword = keyword;
            Text = text;
            Context = context;
        }
    }
}
