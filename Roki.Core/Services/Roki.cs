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
using Roki.Core.Services.Impl;
using Roki.Extensions;
using Roki.Services;
using Victoria;

namespace Roki
{
    public class Roki
    {
        private readonly DbService _db;
        private readonly Logger _log;

        public Roki()
        {
            LogSetup.SetupLogger();
            _log = LogManager.GetCurrentClassLogger();
            // TODO elevated permission check?


            Config = new RokiConfig();
            _db = new DbService(Config);
            _db.Setup();

            Properties = JsonSerializer.Deserialize<Properties>(File.ReadAllText("./data/properties.json"));

            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 500,
                LogLevel = LogSeverity.Warning,
                ConnectionTimeout = int.MaxValue
            });
            CommandService = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async
            });

            OkColor = Color.DarkGreen;
            ErrorColor = Color.Red;

            Client.Log += ClientLog;
        }

        public RokiConfig Config { get; }
        public DiscordSocketClient Client { get; }
        public CommandService CommandService { get; }
        public Properties Properties { get; }
        public static Color OkColor { get; set; }
        public static Color ErrorColor { get; set; }


        public TaskCompletionSource<bool> Ready { get; } = new TaskCompletionSource<bool>();
        public IServiceProvider Services { get; private set; }

        private Task ClientLog(LogMessage arg)
        {
            _log.Warn(arg.Source + " | " + arg.Message);
            if (arg.Exception != null)
                _log.Warn(arg.Exception);

            return Task.CompletedTask;
        }


        private List<ulong> GetCurrentGuildIds()
        {
            return Client.Guilds.Select(x => x.Id).ToList();
        }

        private void AddServices()
        {
//            var startingGuildIdList = GetCurrentGuildIds();
            var sw = Stopwatch.StartNew();

//            var _bot = Client.CurrentUser;

            var service = new ServiceCollection()
                .AddSingleton<IRokiConfig>(Config)
                .AddSingleton(_db)
                .AddSingleton(Client)
                .AddSingleton(CommandService)
                .AddSingleton<LavaConfig>()
                .AddSingleton<LavaNode>()
                .AddSingleton(_db.GetDbContext())
                .AddSingleton(this);

            service.AddHttpClient();

            service.LoadFrom(Assembly.GetAssembly(typeof(CommandHandler)));
            service.LoadFrom(Assembly.GetAssembly(typeof(MessageService)));

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
            _log.Info($"Roki connected in {sw.Elapsed.TotalSeconds}s");

            var stats = Services.GetService<IStatsService>();
            stats.Initialize();
            var commandHandler = Services.GetService<CommandHandler>();
            var commandService = Services.GetService<CommandService>();
            var messageLogger = Services.GetService<MessageService>();

            await commandHandler.StartHandling().ConfigureAwait(false);
            await messageLogger.StartService().ConfigureAwait(false);

            var _ = await commandService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services).ConfigureAwait(false);
            
            Ready.TrySetResult(true);
            _log.Info("Roki is ready");
        }

        private async Task LoginAsync(string token)
        {
            var clientReady = new TaskCompletionSource<bool>();

            Task SetClientReady()
            {
                var _ = Task.Run(async () =>
                {
                    clientReady.TrySetResult(true);
                    try
                    {
                        foreach (var channel in await Client.GetDMChannelsAsync().ConfigureAwait(false))
                            await channel.CloseAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        //
                    }
                });
                return Task.CompletedTask;
            }

            _log.Info("Logging in ...");
            await Client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await Client.StartAsync().ConfigureAwait(false);
            Client.Ready += SetClientReady;
            await clientReady.Task.ConfigureAwait(false);
            Client.Ready -= SetClientReady;
            Client.JoinedGuild += Client_JoinedGuild;
            Client.LeftGuild += Client_LeftGuild;
            _log.Info("Logged in as {0}", Client.CurrentUser);
        }

        private Task Client_LeftGuild(SocketGuild arg)
        {
            _log.Info("Left server: {0} [{1}]", arg?.Name, arg?.Id);
            return Task.CompletedTask;
        }

        private Task Client_JoinedGuild(SocketGuild arg)
        {
            _log.Info("Joined server: {0} [{1}]", arg?.Name, arg?.Id);
            return Task.CompletedTask;
        }
    }
}