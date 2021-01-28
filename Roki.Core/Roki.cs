using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Extensions;
using Roki.Services;
using StackExchange.Redis;
using Victoria;

namespace Roki
{
    public class Roki
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private RokiConfig RokiConfig { get; }
        private DiscordSocketClient Client { get; }
        private CommandService CommandService { get; }
        private IMongoService Mongo { get; }
        private IRedisCache Cache { get; }
        private IConfigurationService Config { get; }
        private IServiceProvider Services { get; set; }

        public static Properties Properties { get; } = new();
        public const ulong BotId = 220678903432347650;

        public Roki()
        {
            LogSetup.SetupLogger();

            RokiConfig = new RokiConfig();
            Mongo = new MongoService(RokiConfig.Db);
            Cache = new RedisCache(RokiConfig.RedisConfig);
            Config = new ConfigurationService(Mongo.Context, Cache);

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

            await LoginAsync(RokiConfig.Token).ConfigureAwait(false);

            Logger.Info("Loading services");

            try
            {
                AddServices();
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }

            sw.Stop();
            Logger.Info("Roki connected in {Elapsed} ms", sw.ElapsedMilliseconds);

            Services.GetService<IStatsService>()!.Initialize();
            await Services.GetService<CommandHandler>()!.StartHandling().ConfigureAwait(false);
            await Services.GetService<CommandService>()!.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services).ConfigureAwait(false);

            await new EventHandlers(Mongo, Client, Cache, Config).StartHandling().ConfigureAwait(false);
        }

        private async Task LoginAsync(string token)
        {
            var ready = new TaskCompletionSource<bool>();

            Logger.Info("Logging in ...");
            Client.Ready += UpdateDatabase;

            await Client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await Client.StartAsync().ConfigureAwait(false);

            await ready.Task.ConfigureAwait(false);
            Client.Ready -= UpdateDatabase;

            Logger.Info("Logged in as {CurrentUser}", Client.CurrentUser);

            // makes sure database contains latest guild/channel info
            Task UpdateDatabase()
            {
                ready.TrySetResult(true);

                Task _ = Task.Run(async () =>
                {
                    try
                    {
                        IDatabase cache = Cache.Redis.GetDatabase();
                        Logger.Info("Loading cache");
                        var sw = Stopwatch.StartNew();

                        foreach (SocketGuild guild in Client.Guilds)
                        {
                            long botCurrency = await Mongo.Context.GetUserCurrency(Client.CurrentUser, guild.Id.ToString());
                            await cache.StringSetAsync($"currency:{guild.Id}:{BotId}", botCurrency, flags: CommandFlags.FireAndForget)
                                .ConfigureAwait(false);

                            SocketGuildUser bot = guild.CurrentUser;
                            SocketRole role = bot.Roles.OrderByDescending(r => r.Position).FirstOrDefault();

                            if (role == null || role.IsEveryone)
                            {
                                await cache.StringSetAsync($"color:{bot.Guild.Id}", Color.Magenta.RawValue).ConfigureAwait(false);
                            }
                            else
                            {
                                await cache.StringSetAsync($"color:{bot.Guild.Id}", role.Color.RawValue).ConfigureAwait(false);
                            }
                            
                            await Mongo.Context.GetOrAddGuildAsync(guild).ConfigureAwait(false);

                            foreach (SocketGuildChannel channel in guild.Channels)
                            {
                                if (!(channel is SocketTextChannel textChannel))
                                {
                                    continue;
                                }

                                await Mongo.Context.GetOrAddChannelAsync(textChannel).ConfigureAwait(false);
                            }
                        }

                        sw.Stop();
                        Logger.Info("Cache loaded in {Elapsed} ms", sw.ElapsedMilliseconds);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }

                    Logger.Info("Roki is ready");
                });

                return Task.CompletedTask;
            }
        }

        private void AddServices()
        {
            var sw = Stopwatch.StartNew();

            IServiceCollection service = new ServiceCollection()
                .AddSingleton(RokiConfig)
                .AddSingleton(Mongo)
                .AddSingleton(Cache)
                .AddSingleton(Config)
                .AddSingleton(Client)
                .AddSingleton(CommandService)
                .AddSingleton<LavaConfig>()
                .AddSingleton<LavaNode>()
                .AddSingleton(this)
                .AddHttpClient()
                .LoadFrom(Assembly.GetAssembly(typeof(CommandHandler)));

            Services = service.BuildServiceProvider();
            LoadTypeReaders(typeof(Roki).Assembly);

            sw.Stop();
            Logger.Info("All services loaded in {Elapsed} ms", sw.ElapsedMilliseconds);
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
                Logger.Warn(ex.LoaderExceptions[0]);
                return;
            }

            foreach (Type type in allTypes)
            {
                if (type.BaseType == null)
                {
                    continue;
                }

                if (!type.IsSubclassOf(typeof(TypeReader)) || type.BaseType.GetGenericArguments().Length <= 0 || type.IsAbstract)
                {
                    continue;
                }

                var typeReader = (TypeReader) Activator.CreateInstance(type, Client, CommandService);
                Type baseType = type.BaseType;
                Type[] typeArgs = baseType?.GetGenericArguments();
                try
                {
                    CommandService.AddTypeReader(typeArgs[0], typeReader);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Unable to load TypeReaders");
                    throw;
                }
            }
        }

        private static Task Log(LogMessage arg)
        {
            Logger.Warn(arg.Source + "|" + arg.Message);
            if (arg.Exception != null)
            {
                Logger.Warn(arg.Exception);
            }

            return Task.CompletedTask;
        }
    }
}