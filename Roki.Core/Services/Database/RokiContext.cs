using Microsoft.EntityFrameworkCore;
using Roki.Services.Database.Models;

namespace Roki.Services.Database
{
    public class RokiContext : DbContext
    {
        private readonly string _connectionString;

        public RokiContext(DbContextOptions options) : base(options)
        {
        }

        public virtual DbSet<Channel> Channels { get; set; }
        public virtual DbSet<ChannelConfig> ChannelConfigs { get; set; }
        public virtual DbSet<Event> Events { get; set; }
        public virtual DbSet<Guild> Guilds { get; set; }
        public virtual DbSet<GuildConfig> GuildConfigs { get; set; }
        public virtual DbSet<InventoryItem> Inventory { get; set; }
        public virtual DbSet<Investment> Investments { get; set; }
        public virtual DbSet<StoreItem> Items { get; set; }
        public virtual DbSet<Message> Messages { get; set; }
        public virtual DbSet<Quote> Quotes { get; set; }
        public virtual DbSet<Subscription> Subscriptions { get; set; }
        public virtual DbSet<Trade> Trades { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserData> UserData { get; set; }
        public virtual DbSet<XpReward> XpRewards { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Clue> Clues { get; set; }
        public virtual DbSet<Round> Rounds { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Channel>(entity =>
            {
                entity.ToTable("channel");

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.DeletedDate)
                    .HasColumnType("date")
                    .HasColumnName("deleted_date");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name");
            });

            modelBuilder.Entity<ChannelConfig>(entity =>
            {
                entity.HasKey(e => e.ChannelId)
                    .HasName("channel_config_pkey");

                entity.ToTable("channel_config");

                entity.Property(e => e.ChannelId)
                    .ValueGeneratedNever()
                    .HasColumnName("channel_id");

                entity.Property(e => e.CurrencyGen).HasColumnName("currency");

                entity.Property(e => e.Logging).HasColumnName("logging");

                entity.Property(e => e.XpGain).HasColumnName("xp");

                entity.HasOne(d => d.Channel)
                    .WithOne(p => p.ChannelConfig)
                    .HasForeignKey<ChannelConfig>(d => d.ChannelId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("channel_config_channel_id_fkey");
            });

            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("event");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.ChannelId).HasColumnName("channel_id");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.HostId).HasColumnName("host_id");

                entity.Property(e => e.MessageId).HasColumnName("message_id");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name");

                entity.Property(e => e.Participants).HasColumnName("participants");

                entity.Property(e => e.StartDate)
                    .HasColumnType("timestamp")
                    .HasColumnName("start_date");

                entity.Property(e => e.Undecided).HasColumnName("undecided");

                entity.HasOne(d => d.Guild)
                    .WithMany(p => p.Events)
                    .HasForeignKey(d => d.GuildId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("event_guild_id_fkey");
            });

            modelBuilder.Entity<Guild>(entity =>
            {
                entity.ToTable("guild");

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.Icon).HasColumnName("icon");

                entity.Property(e => e.Moderators).HasColumnName("moderators");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name");

                entity.Property(e => e.OwnerId).HasColumnName("owner_id");
                entity.Property(e => e.Available).HasColumnName("available");
            });

            modelBuilder.Entity<GuildConfig>(entity =>
            {
                entity.HasKey(e => e.GuildId)
                    .HasName("guild_config_pkey");

                entity.ToTable("guild_config");

                entity.Property(e => e.GuildId)
                    .ValueGeneratedNever()
                    .HasColumnName("guild_id");

                entity.Property(e => e.BetDiceMin).HasColumnName("bd_min");

                entity.Property(e => e.BetFlipMin).HasColumnName("bf_min");

                entity.Property(e => e.BetFlipMultiplier).HasColumnName("bf_multiplier");

                entity.Property(e => e.BetFlipMMinMultiplier).HasColumnName("bfm_min");

                entity.Property(e => e.BetFlipMMinCorrect).HasColumnName("bfm_min_correct");

                entity.Property(e => e.BetFlipMMinGuesses).HasColumnName("bfm_min_guess");

                entity.Property(e => e.BetFlipMMultiplier).HasColumnName("bfm_multiplier");

                entity.Property(e => e.BetRoll100Multiplier).HasColumnName("br_100");

                entity.Property(e => e.BetRoll71Multiplier).HasColumnName("br_71");

                entity.Property(e => e.BetRoll92Multiplier).HasColumnName("br_92");

                entity.Property(e => e.BetRollMin).HasColumnName("br_min");

                entity.Property(e => e.CurrencyGen).HasColumnName("currency");

                entity.Property(e => e.CurrencyGenerationCooldown).HasColumnName("currency_cd");

                entity.Property(e => e.CurrencyGenerationChance).HasColumnName("currency_chance");

                entity.Property(e => e.CurrencyDropAmount).HasColumnName("currency_drop");

                entity.Property(e => e.CurrencyDropAmountMax).HasColumnName("currency_drop_max");

                entity.Property(e => e.CurrencyDropAmountRare).HasColumnName("currency_drop_rare");

                entity.Property(e => e.CurrencyIcon)
                    .IsRequired()
                    .HasColumnName("currency_icon");

                entity.Property(e => e.CurrencyName)
                    .IsRequired()
                    .HasColumnName("currency_name");

                entity.Property(e => e.CurrencyNamePlural)
                    .IsRequired()
                    .HasColumnName("currency_plural");

                entity.Property(e => e.Prefix)
                    .IsRequired()
                    .HasColumnName("prefix");

                entity.Property(e => e.Logging).HasColumnName("logging");

                entity.Property(e => e.NotificationLocation).HasColumnName("notification_location");

                entity.Property(e => e.TriviaEasy).HasColumnName("trivia_easy");

                entity.Property(e => e.TriviaHard).HasColumnName("trivia_hard");

                entity.Property(e => e.TriviaMedium).HasColumnName("trivia_med");

                entity.Property(e => e.TriviaMinCorrect).HasColumnName("trivia_min_correct");

                entity.Property(e => e.XpGain).HasColumnName("xp");

                entity.Property(e => e.XpCooldown).HasColumnName("xp_cd");

                entity.Property(e => e.XpFastCooldown).HasColumnName("xp_fast_cd");

                entity.Property(e => e.XpPerMessage).HasColumnName("xp_per_message");

                entity.Property(e => e.CurrencyDefault).HasColumnName("currency_default");

                entity.Property(e => e.InvestingDefault).HasColumnName("investing_default");

                entity.HasOne(d => d.Guild)
                    .WithOne(p => p.GuildConfig)
                    .HasForeignKey<GuildConfig>(d => d.GuildId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("guild_config_guild_id_fkey");
            });

            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.HasKey(e => new {Uid = e.UserId, e.GuildId, e.ItemId})
                    .HasName("inventory_item_pkey");

                entity.ToTable("inventory_item");

                entity.Property(e => e.UserId).HasColumnName("uid");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.ItemId).HasColumnName("item_id");

                entity.Property(e => e.Quantity).HasColumnName("quantity");
                
                entity.HasOne(d => d.Item)
                    .WithMany(p => p.Inventory)
                    .HasForeignKey(d => d.ItemId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("inventory_item_item_id_fkey");
            });

            modelBuilder.Entity<Investment>(entity =>
            {
                entity.HasKey(e => new {Uid = e.UserId, e.GuildId, e.Symbol})
                    .HasName("investment_pkey");

                entity.ToTable("investment");

                entity.Property(e => e.UserId).HasColumnName("uid");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.Symbol).HasColumnName("symbol");

                entity.Property(e => e.InterestDate)
                    .HasColumnType("date")
                    .HasColumnName("interest_date");

                entity.Property(e => e.Shares).HasColumnName("shares");
                
                entity.HasOne(d => d.User)
                    .WithMany(p => p.Investments)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("investment_uid_fkey");
            });

            modelBuilder.Entity<StoreItem>(entity =>
            {
                entity.ToTable("store_item");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Category).HasColumnName("category");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.Details).HasColumnName("details");

                entity.Property(e => e.Duration).HasColumnName("duration");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name");

                entity.Property(e => e.Price).HasColumnName("price");

                entity.Property(e => e.Quantity).HasColumnName("quantity");

                entity.Property(e => e.SellerId).HasColumnName("seller_id");                              
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("message");

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.Attachments).HasColumnName("attachments");

                entity.Property(e => e.AuthorId).HasColumnName("author_id");

                entity.Property(e => e.ChannelId).HasColumnName("channel_id");

                entity.Property(e => e.Content).HasColumnName("content");

                entity.Property(e => e.Deleted).HasColumnName("deleted");

                entity.Property(e => e.Edits)
                    .HasColumnType("jsonb")
                    .HasColumnName("edits");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.RepliedTo).HasColumnName("replied_to");
            });

            modelBuilder.Entity<Quote>(entity =>
            {
                entity.ToTable("quote");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.AuthorId).HasColumnName("author_id");

                entity.Property(e => e.Context).HasColumnName("context");

                entity.Property(e => e.Date)
                    .HasColumnType("timestamp")
                    .HasColumnName("date");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.Keyword)
                    .IsRequired()
                    .HasColumnName("keyword");

                entity.Property(e => e.Text)
                    .IsRequired()
                    .HasColumnName("text");

                entity.Property(e => e.UseCount).HasColumnName("use_count");
            });

            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => new {Uid = e.UserId, e.GuildId, e.ItemId})
                    .HasName("subscription_pkey");

                entity.ToTable("subscription");

                entity.Property(e => e.UserId).HasColumnName("uid");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.ItemId).HasColumnName("item_id");

                entity.Property(e => e.Expiry)
                    .HasColumnType("date")
                    .HasColumnName("expiry");
                    
                entity.HasOne(d => d.Item)
                    .WithMany(p => p.Subscriptions)
                    .HasForeignKey(d => d.ItemId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("subscription_item_id_fkey");
            });

            modelBuilder.Entity<Trade>(entity =>
            {
                entity.ToTable("trade");

                entity.Property(e => e.Id).HasColumnName("id");
                
                entity.Property(e => e.Shares).HasColumnName("shares");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.Price).HasColumnName("price");

                entity.Property(e => e.Symbol)
                    .IsRequired()
                    .HasColumnName("symbol");

                entity.Property(e => e.Date)
                    .HasColumnType("timestamp")
                    .HasColumnName("date");

                entity.Property(e => e.UserId).HasColumnName("uid");
                
                entity.HasOne(d => d.Investment)
                    .WithMany(p => p.Trades)
                    .HasForeignKey(d => new { d.UserId, d.GuildId, d.Symbol })
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("trade_uid_guild_id_symbol_fkey");
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("transaction");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Amount).HasColumnName("amount");

                entity.Property(e => e.ChannelId).HasColumnName("channel_id");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.MessageId).HasColumnName("message_id");

                entity.Property(e => e.Recipient).HasColumnName("recipient");

                entity.Property(e => e.Sender).HasColumnName("sender");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.Avatar)
                    .IsRequired()
                    .HasColumnName("avatar");

                entity.Property(e => e.Discriminator)
                    .IsRequired()
                    .HasColumnName("discriminator");

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasColumnName("username");
            });

            modelBuilder.Entity<UserData>(entity =>
            {
                entity.HasKey(e => new {Uid = e.UserId, e.GuildId})
                    .HasName("user_data_pkey");

                entity.ToTable("user_data");

                entity.Property(e => e.UserId).HasColumnName("uid");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.Currency).HasColumnName("currency");

                entity.Property(e => e.Investing).HasColumnName("investing");

                entity.Property(e => e.LastLevelUp)
                    .HasColumnType("timestamp")
                    .HasColumnName("last_level_up");

                entity.Property(e => e.LastXpGain)
                    .HasColumnType("timestamp")
                    .HasColumnName("last_xp_gain");

                entity.Property(e => e.NotificationLocation).HasColumnName("notification_location");

                entity.Property(e => e.Xp).HasColumnName("xp");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.UserData)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("user_data_uid_fkey");
            });

            modelBuilder.Entity<XpReward>(entity =>
            {
                entity.ToTable("xp_reward");

                entity.Property(e => e.Id).HasColumnName("id");
                
                entity.Property(e => e.Details).HasColumnName("details");

                entity.Property(e => e.GuildId).HasColumnName("guild_id");

                entity.Property(e => e.Level).HasColumnName("level");

                entity.Property(e => e.Description).HasColumnName("description");
            });
            
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("category", "jeopardy");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name");

                entity.Property(e => e.Round).HasColumnName("round");
            });

            modelBuilder.Entity<Clue>(entity =>
            {
                entity.ToTable("clues", "jeopardy");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Answer)
                    .IsRequired()
                    .HasColumnName("answer");

                entity.Property(e => e.CategoryId)
                    .IsRequired()
                    .HasColumnName("category_id");

                entity.Property(e => e.Text)
                    .IsRequired()
                    .HasColumnName("text");

                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasColumnName("value");

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Clues)
                    .HasForeignKey(d => d.CategoryId)
                    .HasConstraintName("clues_category_id_fkey");
            });

            modelBuilder.Entity<Round>(entity =>
            {
                entity.ToTable("round", "jeopardy");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name");
            });
        }
    }
}