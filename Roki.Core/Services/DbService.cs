using System;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using NLog;
using Roki.Core.Services.Database;

namespace Roki.Core.Services
{
    public class DbService
    {
        private readonly DbContextOptions<RokiContext> _options;

        public DbService(IRokiConfig config)
        {
            var builder = new MySqlConnectionStringBuilder(config.Db.ConnectionString);
            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
            optionsBuilder.UseMySql(builder.ToString());
            _options = optionsBuilder.Options;
        }

        public void Setup()
        {
            using var context = new RokiContext(_options);
            context.Database.EnsureCreated();
            context.SaveChanges();
        }

        private RokiContext GetDbContextInternal()
        {
            var context = new RokiContext(_options);
            context.Database.SetCommandTimeout(60);
            var conn = context.Database.GetDbConnection();
            conn.Open();
            return context;
        }

        public IUnitOfWork GetDbContext()
        {
            return new UnitOfWork(GetDbContextInternal());
        }
    }
}