using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database;

namespace Roki.Core.Services
{
    public class DbService
    {
        private readonly DbContextOptions<RokiContext> _options;

        public DbService(IConfiguration config)
        {
            var builder = new SqliteConnectionStringBuilder(config.Db.ConnectionString);
            builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
            
            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
            _options = optionsBuilder.Options;
            
            optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
            optionsBuilder.UseSqlite(builder.ToString(), x => x.SuppressForeignKeyEnforcement());
        }

        public void Setup()
        {
            using (var context = new RokiContext(_options))
            {
                context.Database.ExecuteSqlCommand("PRAGMA journal_mode=WAL");
                context.SaveChanges();
            }
        }

        private RokiContext GetDbContextInternal()
        {
            var context = new RokiContext(_options);
            context.Database.SetCommandTimeout(60);
            var conn = context.Database.GetDbConnection();
            conn.Open();
            using (var com = conn.CreateCommand())
            {
                com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF";
                com.ExecuteNonQuery();
            }

            return context;
        }
        
        public IUnitOfWork GetDbContext() => new UnitOfWork(GetDbContextInternal());
    }
}