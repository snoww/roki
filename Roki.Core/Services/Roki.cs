using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Core.Extentions;
using Roki.Core.Services;
using Roki.Core.Services.Impl;
using IConfiguration = Roki.Core.Services.IConfiguration;

namespace Roki
{
    public class Roki
    {
        private Logger _log;
        
        public Configuration Config { get; }
        public DiscordSocketClient Client { get; }
        public CommandService CommandService { get; }
        public IServiceProvider Services { get; private set; }
        public Roki()
        {
            LogSetup.SetupLogger();
            _log = LogManager.GetCurrentClassLogger();
         // TODO elevated permission check?
            Config = new Configuration();
            
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 500,
                LogLevel = LogSeverity.Warning,
                ConnectionTimeout = int.MaxValue,
            });
        }
        
        private List<ulong> GetCurrentGuildIds()
        {
            return Client.Guilds.Select(x => x.Id).ToList();
        }

        private void AddServices()
        {
            var startingGuildIdList = GetCurrentGuildIds();
            var sw = Stopwatch.StartNew();

            var _bot = Client.CurrentUser;
            
            var service = new ServiceCollection()
                .AddSingleton<IConfiguration>(Config)
                .AddSingleton(Client)
                .AddSingleton(CommandService)
                .AddSingleton(this);

            service.LoadFrom(Assembly.GetAssembly(typeof(CommandHandler)));

            Services = service.BuildServiceProvider();
            var commandHandler = Services.GetService<CommandHandler>();
            commandHandler.AddS
        }
        
        public static async Task RunAsync(string[] args)
        {
            var roki = new Roki();
            await roki.RunAsync();
        }

        public async Task RunAsync()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
        }

        
    }
}