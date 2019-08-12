using Discord;
using Microsoft.Extensions.Configuration;
using NLog;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System;
using System.Threading;

namespace Roki.Core.Services.Impl
{
    public class Configuration : IConfiguration
    {
        private Logger _log;
        
        public ulong ClientId { get; }
        public string Token { get; }
        public string GoogleApi { get; }
        public string GoogleCs { get; }
        
        public ImmutableArray<ulong> OwnerIds { get; }
        
        private readonly string _credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

        public Configuration()
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
                GoogleCs = data[nameof(GoogleCs)];
                
                if (!ulong.TryParse(data[nameof(ClientId)], out ulong clId))
                    clId = 0;
                ClientId = clId;
            }
            catch (Exception e)
            {
                _log.Fatal(e.Message);
                _log.Fatal(e);
                throw;
            }
        }
      
        public bool IsOwner(IUser u) => OwnerIds.Contains(u.Id);
    }
}