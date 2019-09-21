using System;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Roki.Core.Services.Database;

namespace Roki.Core.Services
{
    public class DbService
    {
        private readonly DbContextOptions<RokiContext> _options;

        public DbService(IRokiConfig config)
        {
            var builder = new MySqlConnectionStringBuilder(config.Db.ConnectionString);

//            var builder = new SqliteConnectionStringBuilder(config.Db.ConnectionString);
//            builder.DataSource = Path.Combine(Directory.GetCurrentDirectory(), builder.DataSource);

            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
            optionsBuilder.UseMySql(builder.ToString());
            _options = optionsBuilder.Options;
        }

        public void Setup()
        {
            using (var context = new RokiContext(_options))
            {
                context.Database.EnsureCreated();
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
//                com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF";
//                com.ExecuteNonQuery();
            }
//            var quotes = context.Quotes;
//            foreach (var quote in quotes)
//            {
//                Console.WriteLine(quote.Keyword + ": " + quote.Text);
//            }

            return context;
        }

        public IUnitOfWork GetDbContext()
        {
            return new UnitOfWork(GetDbContextInternal());
        }
    }
}