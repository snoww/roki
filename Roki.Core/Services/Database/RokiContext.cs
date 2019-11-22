using System;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database
{
    public class RokiContext : DbContext
    {
        public RokiContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Quote> Quotes { get; set; }
        public DbSet<DUser> DUsers { get; set; }
        public DbSet<DMessage> DMessages { get; set; }
        public DbSet<CurrencyTransaction> Transactions { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<Subscriptions> Subscriptions { get; set; }
        public DbSet<Lottery> Lotteries { get; set; }
        public DbSet<Trades> Trades { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region Quotes

            modelBuilder.Entity<Quote>(entity =>
            {
                entity.HasIndex(x => x.GuildId);
                entity.HasIndex(x => x.Keyword);
            });

            #endregion

            #region DUser

            modelBuilder.Entity<DUser>(entity =>
            {
                entity.HasAlternateKey(u => u.UserId);
                entity.Property(u => u.LastLevelUp)
                    .HasDefaultValue(DateTimeOffset.MinValue);
                entity.HasIndex(u => u.TotalXp);
                entity.HasIndex(u => u.Currency);
                entity.HasIndex(u => u.UserId);
            });

            #endregion

            #region DMessage

            modelBuilder.Entity<DMessage>();

            #endregion

            #region CurrencyTransaction

            modelBuilder.Entity<CurrencyTransaction>();

            #endregion

            #region Lottery

            modelBuilder.Entity<Lottery>();

            #endregion

            #region Store

            modelBuilder.Entity<Listing>(entity =>
            {
                entity.HasIndex(s => s.ItemName);
            });

            #endregion

            #region Subscriptions

            modelBuilder.Entity<Subscriptions>();

            #endregion

            #region Trades

            modelBuilder.Entity<Trades>();

            #endregion
        }
    }
}