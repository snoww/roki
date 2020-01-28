using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Extensions;
using StackExchange.Redis;
using Victoria;

namespace Roki.Services
{
    public class Roki
    {
        private readonly DbService _db;
        private readonly Logger _log;

        private RokiConfig Config { get; }
        private DiscordSocketClient Client { get; }
        private CommandService CommandService { get; }
        private IRedisCache Cache { get; }
        public static Properties Properties { get; private set; }
        public static Color OkColor { get; private set; }
        public static Color ErrorColor { get; private set; }

        public TaskCompletionSource<bool> Ready { get; } = new TaskCompletionSource<bool>();
        private IServiceProvider Services { get; set; }

        public Roki()
        {
            LogSetup.SetupLogger();
            _log = LogManager.GetCurrentClassLogger();

            Config = new RokiConfig();
            Cache = new RedisCache(Config.RedisConfig);
            
            _db = new DbService(Config.Db.ConnectionString);
            _db.Setup();

            // global properties
            // future: guild specific properties
            Properties = File.ReadAllText("./data/properties.json").Deserialize<Properties>();

            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                ConnectionTimeout = int.MaxValue,
                ExclusiveBulkDelete = true,
                LogLevel = LogSeverity.Warning
            });
            
            CommandService = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async
            });

            OkColor = Color.DarkGreen;
            ErrorColor = Color.Red;

            Client.Log += Log;
        }
        
        public async Task RunAndBlockAsync()
        {
            await RunAsync().ConfigureAwait(false);
            await Task.Delay(-1).ConfigureAwait(false);
        }

        private async Task RunAsync()
        {
            var sw = Stopwatch.StartNew();

            await LoginAsync(Config.Token).ConfigureAwait(false);

            _log.Info("Loading services");
            
            try
            {
                AddServices();
            }
            catch (Exception e)
            {
                _log.Error(e);
                throw;
            }

            sw.Stop();
            _log.Info($"Roki connected in {sw.ElapsedMilliseconds} ms");

            var stats = Services.GetService<IStatsService>();
            stats.Initialize();
            var commandHandler = Services.GetService<CommandHandler>();
            var commandService = Services.GetService<CommandService>();
            var eventHandlers = Services.GetService<EventHandlers>();

            await commandHandler.StartHandling().ConfigureAwait(false);
            await eventHandlers.StartHandling().ConfigureAwait(false);

            var _ = await commandService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services).ConfigureAwait(false);
            
            Ready.TrySetResult(true);
        }

        private async Task LoginAsync(string token)
        {
            var updateStatus = new TaskCompletionSource<bool>();

            _log.Info("Logging in ...");
            
            await Client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await Client.StartAsync().ConfigureAwait(false);
            
            Client.Ready += UpdateDatabase;
            await updateStatus.Task.ConfigureAwait(false);
            Client.Ready -= UpdateDatabase;
            
            _log.Info("Logged in as {0}", Client.CurrentUser);
            
            // makes sure database contains latest guild/channel info
            Task UpdateDatabase()
            {
                var _ = Task.Run(async () =>
                {
                    updateStatus.TrySetResult(true);
                    try
                    {
                        using var uow = _db.GetDbContext();
                        var cache = Cache.Redis.GetDatabase();
                        await Client.DownloadUsersAsync(Client.Guilds).ConfigureAwait(false);
                        _log.Info("Loading cache");
                        var sw = Stopwatch.StartNew();
                        
                        foreach (var guild in Client.Guilds)
                        {
                            var botCurrency = uow.Users.GetUserCurrency(Properties.BotId);
                            await cache.StringSetAsync($"currency:{guild.Id}:{Properties.BotId}", botCurrency, flags: CommandFlags.FireAndForget)
                                .ConfigureAwait(false);
                            await UpdateCache(cache, guild.Users).ConfigureAwait(false);
                            
                            await uow.Guilds.GetOrCreateGuildAsync(guild).ConfigureAwait(false);
                            foreach (var channel in guild.Channels)
                            {
                                if (channel is SocketTextChannel textChannel)
                                    await uow.Channels.GetOrCreateChannelAsync(textChannel).ConfigureAwait(false);
                            }
                        }

                        sw.Stop();
                        _log.Info($"Cache loaded in {sw.ElapsedMilliseconds} ms");
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                    
                    _log.Info("Roki is ready");
                });

                return Task.CompletedTask;
            }
        }

        private void AddServices()
        {
            var sw = Stopwatch.StartNew();
            
            var service = new ServiceCollection()
                .AddSingleton<IRokiConfig>(Config)
                .AddSingleton(_db)
                .AddSingleton(Cache)
                .AddSingleton(Client)
                .AddSingleton(CommandService)
                .AddSingleton<LavaConfig>()
                .AddSingleton<LavaNode>()
                .AddSingleton(this)
                .AddHttpClient();

            service.LoadFrom(Assembly.GetAssembly(typeof(CommandHandler)));
            service.LoadFrom(Assembly.GetAssembly(typeof(EventHandlers)));

            Services = service.BuildServiceProvider();
            var commandHandler = Services.GetService<CommandHandler>();
            commandHandler.AddServices(service);
            LoadTypeReaders(typeof(Roki).Assembly);

            sw.Stop();
            _log.Info($"All services loaded in {sw.ElapsedMilliseconds} ms");
        }

        private void LoadTypeReaders(Assembly assembly)
        {
            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                _log.Warn(ex.LoaderExceptions[0]);
                return;
            }

            var typeReaders = allTypes
                .Where(x => x.IsSubclassOf(typeof(TypeReader)) && x.BaseType.GetGenericArguments().Length > 0 && !x.IsAbstract);
            
            foreach (var type in typeReaders)
            {
                var typeReader = (TypeReader) Activator.CreateInstance(type, Client, CommandService);
                var baseType = type.BaseType;
                var typeArgs = baseType.GetGenericArguments();
                try
                {
                    CommandService.AddTypeReader(typeArgs[0], typeReader);
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    throw;
                }

            }
        }
        
        private Task Log(LogMessage arg)
        {
            _log.Warn(arg.Source + " | " + arg.Message);
            if (arg.Exception != null)
                _log.Warn(arg.Exception);

            return Task.CompletedTask;
        }

        private Task UpdateCache(IDatabaseAsync cache, IEnumerable<SocketGuildUser> users)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                foreach (var guildUser in users)
                {
                    if (guildUser.IsBot) continue;
                    
                    var user = await uow.Users.GetOrCreateUserAsync(guildUser).ConfigureAwait(false);
                    await cache.StringSetAsync($"currency:{guildUser.Guild.Id}:{guildUser.Id}", user.Currency, flags: CommandFlags.FireAndForget)
                        .ConfigureAwait(false);
                }
            });
            
            return Task.CompletedTask;
        }
    }
}