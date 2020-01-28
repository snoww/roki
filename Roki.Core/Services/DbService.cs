using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roki.Services.Database;

namespace Roki.Services
{
    public class DbService
    {
        private readonly DbContextOptions<RokiContext> _options;

        public DbService(string config)
        {
            var builder = new NpgsqlConnectionStringBuilder(config);
            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
            optionsBuilder.UseNpgsql(builder.ToString())
                .UseSnakeCaseNamingConvention();
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