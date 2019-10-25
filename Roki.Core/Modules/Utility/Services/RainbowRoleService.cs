using System;
using System.Linq;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;

namespace Roki.Modules.Utility.Services
{
    public class RainbowRoleService : IRService
    {
        private readonly DiscordSocketClient _client;
        private Timer _timer;
        private const ulong GuildId = 125025699827417095;
        private readonly Random _rng = new Random();

        public RainbowRoleService(DiscordSocketClient client)
        {
            _client = client; 
            ColorChangeTimer();
        }

        private void ColorChangeTimer()
        {
            _timer = new Timer(ChangeRoleColor, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }
        
        private async void ChangeRoleColor(object state)
        {
            var guild = _client.GetGuild(GuildId);
            var roles = guild.Roles.Where(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var role in roles)
            {
                await role.ModifyAsync(r => r.Color = new Color(_rng.Next(0, 256), _rng.Next(0, 256), _rng.Next(0, 256))).ConfigureAwait(false);
            }
        }
    }
}