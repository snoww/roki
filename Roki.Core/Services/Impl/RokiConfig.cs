using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Discord;
using Microsoft.Extensions.Configuration;
using NLog;

namespace Roki.Core.Services.Impl
{
    public class RokiConfig : IRokiConfig
    {
        private readonly string _credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
        private readonly Logger _log;
        
        public ulong ClientId { get; }
        public string Token { get; }
        public string GoogleApi { get; }
        public string OmdbApi { get; }
        public string DarkSkyApi { get; }
        public string TwitterConsumer { get; }
        public string TwitterConsumerSecret { get; }
        public string TwitterAccessToken { get; }
        public string TwitterAccessSecret { get; }
        public ImmutableArray<ulong> OwnerIds { get; }

        public DbConfig Db { get; }

        public bool IsOwner(IUser u)
        {
            return OwnerIds.Contains(u.Id);
        }

        public RokiConfig()
        {
            _log = LogManager.GetCurrentClassLogger();
            try
            {
//                Console.WriteLine($"The current directory is {_credsFileName}");
                var configBuilder = new ConfigurationBuilder();
                configBuilder.AddJsonFile(_credsFileName, true)
                    .AddEnvironmentVariables("Roki_");

                var data = configBuilder.Build();

                Token = data[nameof(Token)];
                if (string.IsNullOrWhiteSpace(Token))
                {
                    _log.Error("Token is missing from config.json or Environment varibles. Add it and restart the program.");
                    if (!Console.IsInputRedirected)
                        Console.ReadKey();
                    Environment.Exit(3);
                }

                OwnerIds = data.GetSection("OwnerIds").GetChildren().Select(c => ulong.Parse(c.Value)).ToImmutableArray();
                GoogleApi = data[nameof(GoogleApi)];
                OmdbApi = data[nameof(OmdbApi)];
                DarkSkyApi = data[nameof(DarkSkyApi)];
                TwitterConsumer = data[nameof(TwitterConsumer)];
                TwitterConsumerSecret = data[nameof(TwitterConsumerSecret)];
                TwitterAccessToken = data[nameof(TwitterAccessToken)];
                TwitterAccessSecret = data[nameof(TwitterAccessSecret)];

                if (!ulong.TryParse(data[nameof(ClientId)], out var clId))
                    clId = 0;
                ClientId = clId;

                var dbSection = data.GetSection("db");
                Db = new DbConfig(string.IsNullOrWhiteSpace(dbSection["Type"])
                        ? "mysql"
                        : dbSection["Type"],
                    string.IsNullOrWhiteSpace(dbSection["ConnectionString"])
                        ? "server=localhost;database=roki;user=roki;password=roki-snow"
                        : dbSection["ConnectionString"]);
            }
            catch (Exception e)
            {
                _log.Fatal(e.Message);
                _log.Fatal(e);
                throw;
            }
        }
    }
}