using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roki.Core.Services.Database;

namespace Roki.Core.Services
{
    public class DbService
    {
        private readonly DbContextOptions<RokiContext> _options;

        public DbService(IRokiConfig config)
        {
            var builder = new NpgsqlConnectionStringBuilder(config.Db.ConnectionString);
            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
            optionsBuilder.UseNpgsql(builder.ToString());
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