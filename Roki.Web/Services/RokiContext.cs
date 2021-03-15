using Microsoft.EntityFrameworkCore;
using Roki.Web.Models;

namespace Roki.Web.Services
{
    public class RokiContext : DbContext
    {
        public RokiContext(DbContextOptions options) : base(options)
        {
        }

        public virtual DbSet<Guild> Guilds { get; set; }
        public virtual DbSet<GuildConfig> GuildConfigs { get; set; }
        public virtual DbSet<Channel> Channels { get; set; }
        public virtual DbSet<ChannelConfig> ChannelConfigs { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Channel>(entity =>
            {
                entity.ToTable("channel", "roki");
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
                entity.ToTable("channel_config", "roki");
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
            modelBuilder.Entity<Guild>(entity =>
            {
                entity.ToTable("guild", "roki");
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
                entity.ToTable("guild_config", "roki");
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
                entity.Property(e => e.JeopardyWinMultiplier).HasColumnName("jeopardy_multiplier");
                entity.Property(e => e.ShowHelpOnError).HasColumnName("show_help");

                entity.HasOne(d => d.Guild)
                    .WithOne(p => p.GuildConfig)
                    .HasForeignKey<GuildConfig>(d => d.GuildId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("guild_config_guild_id_fkey");
            });
        }
    }
}