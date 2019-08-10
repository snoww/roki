using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace Roki.Core.Services
{
    public class StartupService
    {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;

        public StartupService(
            IServiceProvider provider,
            DiscordSocketClient client,
            CommandService commands,
            IConfigurationRoot config)
        {
            _provider = provider;
            _client = client;
            _commands = commands;
            _config = config;
        }

        public async Task StartAsync()
        {
            string token = _config["token:discord"];
            
            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("No bot token found");
            
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            
//            await _commands.AddModuleAsync(Assembly.GetEntryAssembly(), _provider);
        }
    }
}