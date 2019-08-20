using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Core.Services.Impl;

namespace Roki.Core.Services.Database
{
    public class RokiContextFactory : IDesignTimeDbContextFactory<RokiContext>
    {
        public RokiContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>(); 
            IConfiguration config = new Configuration();
            var builder = new SqliteConnectionStringBuilder(config.Db.ConnectionString);
            builder.DataSource = Path.Combine(Directory.GetCurrentDirectory(), builder.DataSource);
            optionsBuilder.UseSqlite(builder.ToString());
            var ctx = new RokiContext(optionsBuilder.Options);
            ctx.Database.SetCommandTimeout(60);
            return ctx;
        }
    }
    public class RokiContext : DbContext
    {
        public DbSet<Quote> Quotes { get; set; }

        public RokiContext(DbContextOptions<RokiContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region QUOTES

            var quoteEntity = modelBuilder.Entity<Quote>();
            quoteEntity.HasIndex(x => x.GuildId);
            quoteEntity.HasIndex(x => x.Keyword);

            #endregion
        }
    }
}