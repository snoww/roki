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
using MongoDB.Driver;
using NLog;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;
using StackExchange.Redis;
using Victoria;

namespace Roki
{
    public class Roki
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public Roki()
        {
            LogSetup.SetupLogger();

            Config = new RokiConfig();
            Mongo = new MongoService(Config.Db);
            Cache = new RedisCache(Config.RedisConfig);

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

        private RokiConfig Config { get; }
        private DiscordSocketClient Client { get; }
        private CommandService CommandService { get; }
        private IMongoService Mongo { get; }
        private IRedisCache Cache { get; }
        private IServiceProvider Services { get; set; }

        public static Properties Properties { get; private set; }
        public static Color OkColor { get; private set; }
        public static Color ErrorColor { get; private set; }

        public async Task RunAndBlockAsync()
        {
            await RunAsync().ConfigureAwait(false);
            await Task.Delay(-1).ConfigureAwait(false);
        }

        private async Task RunAsync()
        {
            var sw = Stopwatch.StartNew();

            await LoginAsync(Config.Token).ConfigureAwait(false);

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

            await new EventHandlers(Mongo, Client, Cache).StartHandling().ConfigureAwait(false);
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
                        long botCurrency = Mongo.Context.GetBotCurrencyAsync();

                        IDatabase cache = Cache.Redis.GetDatabase();
                        Logger.Info("Loading cache");
                        var sw = Stopwatch.StartNew();

                        foreach (SocketGuild guild in Client.Guilds)
                        {
                            await cache.StringSetAsync($"currency:{guild.Id}:{Properties.BotId}", botCurrency, flags: CommandFlags.FireAndForget)
                                .ConfigureAwait(false);
                            await UpdateCache(cache, guild.Users).ConfigureAwait(false);

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
                .AddSingleton<IRokiConfig>(Config)
                .AddSingleton(Mongo)
                .AddSingleton(Cache)
                .AddSingleton(Client)
                .AddSingleton(CommandService)
                .AddSingleton<LavaConfig>()
                .AddSingleton<LavaNode>()
                .AddSingleton(this)
                .AddHttpClient();

            service.LoadFrom(Assembly.GetAssembly(typeof(CommandHandler)));

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

        private Task UpdateCache(IDatabaseAsync cache, IEnumerable<SocketGuildUser> users)
        {
            Task _ = Task.Run(async () =>
            {
                IMongoCollection<User> collection = Mongo.Database.GetCollection<User>("users");
                foreach (SocketGuildUser guildUser in users)
                {
                    if (guildUser.Id == Client.CurrentUser.Id)
                    {
                        SocketRole role = guildUser.Roles.OrderByDescending(r => r.Position).FirstOrDefault();

                        if (role == null || role.IsEveryone)
                        {
                            await cache.StringSetAsync($"color:{guildUser.Guild.Id}", Color.Magenta.RawValue).ConfigureAwait(false);
                        }
                        else
                        {
                            await cache.StringSetAsync($"color:{guildUser.Guild.Id}", role.Color.RawValue).ConfigureAwait(false);
                        }

                        continue;
                    }

                    if (guildUser.IsBot)
                    {
                        continue;
                    }

                    UpdateDefinition<User> userUpdate = Builders<User>.Update.Set(u => u.Id, guildUser.Id)
                        .Set(u => u.Username, guildUser.Username)
                        .Set(u => u.Discriminator, int.Parse(guildUser.Discriminator))
                        .Set(u => u.AvatarId, guildUser.AvatarId);

                    User user = await collection.FindOneAndUpdateAsync<User>(u => u.Id == guildUser.Id, userUpdate,
                        new FindOneAndUpdateOptions<User> {ReturnDocument = ReturnDocument.After, IsUpsert = true}).ConfigureAwait(false);

                    await cache.StringSetAsync($"currency:{guildUser.Guild.Id}:{guildUser.Id}", user.Currency, flags: CommandFlags.FireAndForget)
                        .ConfigureAwait(false);
                }
            });

            return Task.CompletedTask;
        }
    }
}