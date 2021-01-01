﻿using System;
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
using Roki.Services.Database;
using Roki.Services.Database.Maps;
using StackExchange.Redis;
using Victoria;

namespace Roki
{
    public class Roki
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private RokiConfig Config { get; }
        private DiscordSocketClient Client { get; }
        private CommandService CommandService { get; }
        private IMongoService Mongo { get; }
        private IRedisCache Cache { get; }
        private IServiceProvider Services { get; set; }

        public static Properties Properties { get; private set; }
        public static Color OkColor { get; private set; }
        public static Color ErrorColor { get; private set; }

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
            Logger.Info("Roki connected in {elapsed} ms", sw.ElapsedMilliseconds);

            var stats = Services.GetService<IStatsService>();
            stats.Initialize();
            var commandHandler = Services.GetService<CommandHandler>();
            var commandService = Services.GetService<CommandService>();
            
            await commandHandler.StartHandling().ConfigureAwait(false);
            await new EventHandlers(Mongo, Client, Cache).StartHandling().ConfigureAwait(false);

            await commandService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services).ConfigureAwait(false);
        }

        private async Task LoginAsync(string token)
        {
            var ready = new TaskCompletionSource<bool>();

            Logger.Info("Logging in ...");
            
            await Client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await Client.StartAsync().ConfigureAwait(false);
            
            Client.Ready += UpdateDatabase;
            await ready.Task.ConfigureAwait(false);
            Client.Ready -= UpdateDatabase;
            
            Logger.Info("Logged in as {currentuser}", Client.CurrentUser);
            
            // makes sure database contains latest guild/channel info
            Task UpdateDatabase()
            {
                ready.TrySetResult(true);

                var _ = Task.Run(async () =>
                {
                    try
                    {
                        var botCurrency = Mongo.Context.GetBotCurrencyAsync();
                        
                        var cache = Cache.Redis.GetDatabase();
                        await Client.DownloadUsersAsync(Client.Guilds).ConfigureAwait(false);
                        Logger.Info("Loading cache");
                        var sw = Stopwatch.StartNew();
                        
                        foreach (var guild in Client.Guilds)
                        {
                            await cache.StringSetAsync($"currency:{guild.Id}:{Properties.BotId}", botCurrency, flags: CommandFlags.FireAndForget)
                                .ConfigureAwait(false);
                            await UpdateCache(cache, guild.Users).ConfigureAwait(false);

                            await Mongo.Context.GetOrAddGuildAsync(guild).ConfigureAwait(false);

                            foreach (var channel in guild.Channels)
                            {
                                if (!(channel is SocketTextChannel textChannel)) 
                                    continue;

                                await Mongo.Context.GetOrAddChannelAsync(textChannel).ConfigureAwait(false);
                            }
                        }

                        sw.Stop();
                        Logger.Info("Cache loaded in {elapsed} ms", sw.ElapsedMilliseconds);
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
            
            var service = new ServiceCollection()
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
            Logger.Info("All services loaded in {elapsed} ms", sw.ElapsedMilliseconds);
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

            foreach (var type in allTypes)
            {
                if (type.BaseType == null) 
                    continue;
                if (!type.IsSubclassOf(typeof(TypeReader)) || type.BaseType.GetGenericArguments().Length <= 0 || type.IsAbstract)
                    continue;
                
                var typeReader = (TypeReader) Activator.CreateInstance(type, Client, CommandService);
                var baseType = type.BaseType;
                var typeArgs = baseType?.GetGenericArguments();
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
                Logger.Warn(arg.Exception);

            return Task.CompletedTask;
        }

        private Task UpdateCache(IDatabaseAsync cache, IEnumerable<SocketGuildUser> users)
        {
            var _ = Task.Run(async () =>
            {
                var collection = Mongo.Database.GetCollection<User>("users");
                foreach (var guildUser in users)
                {
                    if (guildUser.Id == Client.CurrentUser.Id)
                    {
                        var role = guildUser.Roles.OrderByDescending(r => r.Position).FirstOrDefault();

                        if (role == null)
                        {
                            await cache.StringSetAsync($"color:{guildUser.Guild.Id}", Color.Green.RawValue).ConfigureAwait(false);
                        }
                        else
                        {
                            await cache.StringSetAsync($"color:{guildUser.Guild.Id}", role.Color.RawValue).ConfigureAwait(false);
                        }
                        
                        continue;
                    }
                    
                    if (guildUser.IsBot)
                        continue;

                    var userUpdate = Builders<User>.Update.Set(u => u.Id, guildUser.Id)
                        .Set(u => u.Username, guildUser.Username)
                        .Set(u => u.Discriminator, int.Parse(guildUser.Discriminator))
                        .Set(u => u.AvatarId, guildUser.AvatarId);

                    var user = await collection.FindOneAndUpdateAsync<User>(u => u.Id == guildUser.Id, userUpdate, 
                        new FindOneAndUpdateOptions<User>{ReturnDocument = ReturnDocument.After, IsUpsert = true}).ConfigureAwait(false);
                    
                    await cache.StringSetAsync($"currency:{guildUser.Guild.Id}:{guildUser.Id}", user.Currency, flags: CommandFlags.FireAndForget)
                        .ConfigureAwait(false);
                }
            });
            
            return Task.CompletedTask;
        }
    }
}