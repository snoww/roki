using System;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database
{
//    public class RokiContextFactory : IDesignTimeDbContextFactory<RokiContext>
//    {
//        public RokiContext CreateDbContext(string[] args)
//        {
//            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
//            IRokiConfig config = new RokiConfig();
//            var builder = new SqliteConnectionStringBuilder(config.Db.ConnectionString);
//            builder.DataSource = Path.Combine(Directory.GetCurrentDirectory(), builder.DataSource);
//            optionsBuilder.UseSqlite(builder.ToString());
//            var ctx = new RokiContext(optionsBuilder.Options);
//            ctx.Database.SetCommandTimeout(60);
//            return ctx;
//        }
//    }

    public class RokiContext : DbContext
    {
        public RokiContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Quote> Quotes { get; set; }
        public DbSet<DUser> DUsers { get; set; }
        public DbSet<DMessage> DMessages { get; set; }

//        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//        {
//            optionsBuilder.UseMySQL("server=localhost;database=roki;user=roki;password=roki");
//        }

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