using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;
using Victoria;

namespace Roki
{
    public class Roki
    {
        private readonly DbService _db;
        private readonly Logger _log;

        private RokiConfig Config { get; }
        private DiscordSocketClient Client { get; }
        private CommandService CommandService { get; }
        private IRedisCache Cache { get; set; }
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
            
            _db = new DbService(Config);
            _db.Setup();

            // global properties
            // future: guild specific properties
            Properties = File.ReadAllText("./data/properties.json").Deserialize<Properties>();

            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                ConnectionTimeout = int.MaxValue,
                ExclusiveBulkDelete = true
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

        private Task Log(LogMessage arg)
        {
            _log.Warn(arg.Source + " | " + arg.Message);
            if (arg.Exception != null)
                _log.Warn(arg.Exception);

            return Task.CompletedTask;
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
            _log.Info($"All services loaded in {sw.Elapsed.TotalSeconds:F2}s");
        }

        private IEnumerable<object> LoadTypeReaders(Assembly assembly)
        {
            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                _log.Warn(ex.LoaderExceptions[0]);
                return Enumerable.Empty<object>();
            }

            var filteredTypes = allTypes
                .Where(x => x.IsSubclassOf(typeof(TypeReader))
                            && x.BaseType.GetGenericArguments().Length > 0
                            && !x.IsAbstract);

            var toReturn = new List<object>();
            foreach (var filteredType in filteredTypes)
            {
                var x = (TypeReader) Activator.CreateInstance(filteredType, Client, CommandService);
                var baseType = filteredType.BaseType;
                var typeArgs = baseType.GetGenericArguments();
                try
                {
                    CommandService.AddTypeReader(typeArgs[0], x);
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    throw;
                }

                toReturn.Add(x);
            }

            return toReturn;
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
            _log.Info($"Roki connected in {sw.Elapsed.TotalSeconds:F2}s");

            var stats = Services.GetService<IStatsService>();
            stats.Initialize();
            var commandHandler = Services.GetService<CommandHandler>();
            var commandService = Services.GetService<CommandService>();
            var eventHandlers = Services.GetService<EventHandlers>();

            await commandHandler.StartHandling().ConfigureAwait(false);
            await eventHandlers.StartHandling().ConfigureAwait(false);

            var _ = await commandService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services).ConfigureAwait(false);
            
            Ready.TrySetResult(true);
            _log.Info("Roki is ready");
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
                        foreach (var guild in Client.Guilds)
                        {
                            await uow.Guilds.GetOrCreateGuildAsync(guild).ConfigureAwait(false);
                            foreach (var channel in guild.Channels)
                            {
                                if (channel is SocketTextChannel textChannel)
                                    await uow.Channels.GetOrCreateChannelAsync(textChannel).ConfigureAwait(false);
                            }
                        }

                        await uow.SaveChangesAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                });
                return Task.CompletedTask;
            }
        }
    }
}