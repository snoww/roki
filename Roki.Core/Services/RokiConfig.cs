using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Discord;
using Microsoft.Extensions.Configuration;
using NLog;

namespace Roki.Services
{
    public interface IRokiConfig
    {
        ulong ClientId { get; }
        string Token { get; }
        string GoogleApi { get; }
        string OmdbApi { get; }
        string DarkSkyApi { get; }
        string TwitterConsumer { get; }
        string TwitterConsumerSecret { get; }
        string TwitterAccessToken { get; }
        string TwitterAccessSecret { get; }
        string IexToken { get; }
        string WolframAlphaApi { get; }
        string NavigraphUsername { get; }
        string NavigraphPassword { get; }

        ImmutableArray<ulong> OwnerIds { get; }

        string RedisConfig { get; }
        DbConfig Db { get; }

        bool IsOwner(IUser user);
    }
    
    public class RokiConfig : IRokiConfig
    {
        private readonly string _config = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public ulong ClientId { get; }
        public string Token { get; }
        public string GoogleApi { get; }
        public string OmdbApi { get; }
        public string DarkSkyApi { get; }
        public string TwitterConsumer { get; }
        public string TwitterConsumerSecret { get; }
        public string TwitterAccessToken { get; }
        public string TwitterAccessSecret { get; }
        public string IexToken { get; }
        public string WolframAlphaApi { get; }
        public string NavigraphUsername { get; }
        public string NavigraphPassword { get; }
        public ImmutableArray<ulong> OwnerIds { get; }

        public string RedisConfig { get; }
        public DbConfig Db { get; }

        public bool IsOwner(IUser user)
        {
            return OwnerIds.Contains(user.Id);
        }

        public RokiConfig()
        {
            try
            {
                var configBuilder = new ConfigurationBuilder();
                configBuilder.AddJsonFile(_config, true);
                IConfigurationRoot data = configBuilder.Build();

                Token = data[nameof(Token)];
                if (string.IsNullOrWhiteSpace(Token))
                {
                    Logger.Error("Token is missing from config.json.");
                    if (!Console.IsInputRedirected)
                        Console.ReadKey();
                    Environment.Exit(-1);
                }

                OwnerIds = data.GetSection("OwnerIds").GetChildren().Select(c => ulong.Parse(c.Value)).ToImmutableArray();
                GoogleApi = data[nameof(GoogleApi)];
                OmdbApi = data[nameof(OmdbApi)];
                DarkSkyApi = data[nameof(DarkSkyApi)];
                TwitterConsumer = data[nameof(TwitterConsumer)];
                TwitterConsumerSecret = data[nameof(TwitterConsumerSecret)];
                TwitterAccessToken = data[nameof(TwitterAccessToken)];
                TwitterAccessSecret = data[nameof(TwitterAccessSecret)];
                IexToken = data[nameof(IexToken)];
                WolframAlphaApi = data[nameof(WolframAlphaApi)];
                NavigraphUsername = data[nameof(NavigraphUsername)];
                NavigraphPassword = data[nameof(NavigraphPassword)];

                if (!ulong.TryParse(data[nameof(ClientId)], out var clId))
                    clId = 0;
                ClientId = clId;

                IConfigurationSection dbSection = data.GetSection("db");
                Db = new DbConfig(dbSection["Username"], dbSection["Password"], dbSection["Database"], dbSection["Host"]);

                RedisConfig = "localhost";
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Unable to load configuration.");
                throw;
            }
        }
    }
    
    public class DbConfig
    {
        public DbConfig(string username, string password, string database, string host)
        {
            Username = username;
            Password = password;
            Database = database;
            Host = host;
        }

        public string Username { get; }
        public string Password { get; }
        public string Database { get; }
        public string Host { get; set; }
    }
}