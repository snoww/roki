﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using Victoria;
using IDatabase = StackExchange.Redis.IDatabase;

namespace Roki
{
    public class Roki
    {
        public const uint OkColor = 0xFF00FF;
        public const uint ErrorColor = 0xFF0000;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private IRokiConfig RokiConfig { get; }
        private DiscordSocketClient Client { get; }
        private CommandService CommandService { get; }
        private IRedisCache Cache { get; }
        private IServiceProvider Services { get; set; }

        public static Properties Properties { get; } = new();
        public static ulong BotId { get; private set; }

        public Roki()
        {
            LogSetup.SetupLogger();

            RokiConfig = new RokiConfig();
            Cache = new RedisCache();

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
            
            try
            {
                Logger.Info("Loading services");
                AddServices();
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }

            await LoginAsync(RokiConfig.Token).ConfigureAwait(false);

            sw.Stop();
            Logger.Info("Roki connected in {Elapsed} s", sw.Elapsed.TotalSeconds);
            Logger.Info("Loading database and cache");

            Services.GetRequiredService<IStatsService>().Initialize();
            await Services.GetRequiredService<CommandHandler>().StartHandling().ConfigureAwait(false);
            await Services.GetRequiredService<CommandService>().AddModulesAsync(GetType().GetTypeInfo().Assembly, Services).ConfigureAwait(false);
            await Services.GetRequiredService<EventHandlers>().StartHandling().ConfigureAwait(false);
        }
        
        private void AddServices()
        {
            var sw = Stopwatch.StartNew();

            IServiceCollection service = new ServiceCollection()
                .AddDbContext<RokiContext>(options => options.UseNpgsql(RokiConfig.Db.ToString()))
                .AddSingleton<IConfigurationService, ConfigurationService>()
                .AddSingleton<ICurrencyService, CurrencyService>()
                .AddSingleton(RokiConfig)
                .AddSingleton(Cache)
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
            Logger.Info("Loaded {count} services in {elapsed} ms", service.Count, sw.ElapsedMilliseconds);
        }

        private async Task LoginAsync(string token)
        {
            var ready = new TaskCompletionSource<bool>();

            Logger.Info("Logging in ...");
            var dbSw = Stopwatch.StartNew();
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
                    using IServiceScope scope = Services.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                    await using IDbContextTransaction trans = await context.Database.BeginTransactionAsync();
                    try
                    {
                        BotId = Client.CurrentUser.Id;
                        IDatabase cache = Cache.Redis.GetDatabase();
                        if (!await context.Users.AsQueryable().AnyAsync(x => x.Id == BotId))
                        {
                            await context.Users.AddAsync(new User(Client.CurrentUser.Id, Client.CurrentUser.Username, Client.CurrentUser.Discriminator, Client.CurrentUser.AvatarId));
                        }

                        foreach (SocketGuild guild in Client.Guilds)
                        {
                            SocketGuildUser bot = guild.CurrentUser;
                            SocketRole role = bot.Roles.OrderByDescending(r => r.Position).FirstOrDefault();

                            if (role == null || role.IsEveryone)
                            {
                                await cache.StringSetAsync($"color:{bot.Guild.Id}", OkColor).ConfigureAwait(false);
                            }
                            else
                            {
                                await cache.StringSetAsync($"color:{bot.Guild.Id}", role.Color.RawValue).ConfigureAwait(false);
                            }

                            if (!await context.Guilds.AsQueryable().AnyAsync(x => x.Id == guild.Id))
                            {
                                if (!await context.Users.AsQueryable().AnyAsync(x => x.Id == guild.OwnerId))
                                {
                                    await context.Users.AddAsync(new User(guild.OwnerId, guild.Owner.Username, guild.Owner.Discriminator, guild.Owner.AvatarId));
                                    await context.SaveChangesAsync();
                                }

                                await context.Guilds.AddAsync(new Guild(guild.Id, guild.Name, guild.IconId, guild.OwnerId)
                                {
                                    GuildConfig = new GuildConfig()
                                });
                                
                                await context.UserData.AddRangeAsync(new UserData(BotId, guild.Id), new UserData(guild.OwnerId, guild.Id));
                                
                                await context.SaveChangesAsync();
                                await context.Channels.AddRangeAsync(guild.TextChannels.Select(channel => new Channel(channel.Id, guild.Id, channel.Name) { ChannelConfig = new ChannelConfig()}).ToList());
                            }
                        }

                        await context.SaveChangesAsync();
                        await trans.CommitAsync();

                        dbSw.Stop();
                        Logger.Info("Finished loading database and cache in {Elapsed} s", dbSw.Elapsed.TotalSeconds);
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
                try
                {
                    CommandService.AddTypeReader(type.BaseType?.GetGenericArguments()[0], typeReader);
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