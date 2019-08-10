using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Common.ModuleBehaviors;
using Roki.Extentions;
using Roki.Core.Services.Impl;

namespace Roki.Core.Services
{
    public class GuildUserComparer : IEqualityComparer<IGuildUser>
    {
        public bool Equals(IGuildUser x, IGuildUser y) => x.Id == y.Id;

        public int GetHashCode(IGuildUser obj) => obj.Id.GetHashCode();
    }
    
    public class CommandHandler : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        private readonly Logger _log;
//        private readonly Configuration _config;
        private readonly Roki _roki;
        private IServiceProvider _services;
        private IEnumerable<IEarlyBehavior> _earlyBehaviors;
        private IEnumerable<IInputTransformer> _inputTransformers;
        private IEnumerable<ILateBlocker> _lateBlockers;
        private IEnumerable<ILateExecutor> _lateExecutors;
        
        private const float OneThousandth = 1.0f / 1000;

        
        public string DefaultPrefix { get; set; }

        public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };
        public event Func<CommandInfo, ITextChannel, string, Task> CommandErrored = delegate { return Task.CompletedTask; };
        public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };
        
        public ConcurrentDictionary<ulong, uint> UserMessagesSent { get; } = new ConcurrentDictionary<ulong, uint>();

        public CommandHandler(DiscordSocketClient client,
            CommandService commandService,
//            Configuration config,
            Roki roki,
            IServiceProvider services)
        {
            _client = client;
            _commandService = commandService;
//            _config = config;
            _roki = roki;
            _services = services;

            _log = LogManager.GetCurrentClassLogger();

            DefaultPrefix = ".";
        }

        public void AddServices(IServiceCollection services)
        {
            _lateBlockers = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(ILateBlocker)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as ILateBlocker)
                .ToArray();

            _lateExecutors = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(ILateExecutor)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as ILateExecutor)
                .ToArray();

            _inputTransformers = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(IInputTransformer)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as IInputTransformer)
                .ToArray();

            _earlyBehaviors = services.Where(x => x.ImplementationType?.GetInterfaces().Contains(typeof(IEarlyBehavior)) ?? false)
                .Select(x => _services.GetService(x.ImplementationType) as IEarlyBehavior)
                .ToArray();
        }
        
        public Task StartHandling()
        {
            _client.MessageReceived += (msg) => { var _ = Task.Run(() => MessageReceivedHandler(msg)); return Task.CompletedTask; };
            return Task.CompletedTask;
        }

        public async Task ExecuteExternal(ulong? guildId, ulong channelId, string commandText)
        {
            if (guildId != null)
            {
                var guild = _client.GetGuild(guildId.Value);
                if (!(guild?.GetChannel(channelId) is SocketTextChannel channel))
                {
                    _log.Warn("Channel for external execution not found");
                    return;
                }

                try
                {
                    IUserMessage msg = await channel.SendMessageAsync(commandText).ConfigureAwait(false);
                    msg = (IUserMessage) await channel.GetMessageAsync(msg.Id).ConfigureAwait(false);
                    await TryRunComand(guild, channel, msg).ConfigureAwait(false);
                }
                catch
                {
                    //
                }
            }
        }
        
        private Task LogSuccessfulExecution(IUserMessage usrMsg, ITextChannel channel, params int[] execPoints)
        {
            _log.Info($"Command Executed after " + string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))) + "s\n\t" +
                      "User: {0}\n\t" +
                      "Server: {1}\n\t" +
                      "Channel: {2}\n\t" +
                      "Message: {3}",
                usrMsg.Author + " [" + usrMsg.Author.Id + "]", // {0}
                (channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]"), // {1}
                (channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]"), // {2}
                usrMsg.Content // {3}
            );

//            _log.Info("Succ | g:{0} | c: {1} | u: {2} | msg: {3}",
//                    channel?.Guild.Id.ToString() ?? "-",
//                    channel?.Id.ToString() ?? "-",
//                    usrMsg.Author.Id,
//                    usrMsg.Content.TrimTo(10));
            return Task.CompletedTask;
        }
        
        private void LogErroredExecution(string errorMessage, IUserMessage usrMsg, ITextChannel channel, params int[] execPoints)
        {
            _log.Warn($"Command Errored after " + string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))) + "s\n\t" +
                      "User: {0}\n\t" +
                      "Server: {1}\n\t" +
                      "Channel: {2}\n\t" +
                      "Message: {3}\n\t" +
                      "Error: {4}",
                usrMsg.Author + " [" + usrMsg.Author.Id + "]", // {0}
                (channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]"), // {1}
                (channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]"), // {2}
                usrMsg.Content,// {3}
                errorMessage
                //exec.Result.ErrorReason // {4}
            );

//            _log.Warn("Err | g:{0} | c: {1} | u: {2} | msg: {3}\n\tErr: {4}",
//                    channel?.Guild.Id.ToString() ?? "-",
//                    channel?.Id.ToString() ?? "-",
//                    usrMsg.Author.Id,
//                    usrMsg.Content.TrimTo(10),
//                    errorMessage);
        }

        private async Task MessageReceivedHandler(SocketMessage msg)
        {
            try
            {
                if (msg.Author.IsBot || !_roki.Ready.Task.IsCompleted)
                    return;
                if (!(msg is SocketUserMessage usrMsg))
                    return;

                UserMessagesSent.AddOrUpdate(usrMsg.Author.Id, 1, (key, old) => ++old);

                var channel = msg.Channel as ISocketMessageChannel;
                var guild = (msg.Channel as SocketTextChannel)?.Guild;

                await TryRunComand(guild, channel, usrMsg).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.Warn("Error in CommandHandler");
                _log.Warn(e);
                if (e.InnerException != null)
                {
                    _log.Warn("Inner Exception of the error in CommandHandler");
                    _log.Warn(e.InnerException);
                }

            }
        }

        public async Task TryRunComand(SocketGuild guild, ISocketMessageChannel channel, IUserMessage usrMsg)
        {
            var execTime = Environment.TickCount;

            foreach (var behavior in _earlyBehaviors)
            {
                if (await behavior.RunBehavior(_client, guild, usrMsg).ConfigureAwait(false))
                {
                    if (behavior.BehaviorType == ModuleBehaviorType.Blocker)
                    {
                        _log.Info("Blocked User: [{0}] Message: [{1}] Service: [{2}]", usrMsg.Author, usrMsg.Content, behavior.GetType().Name);
                    }
                    else if (behavior.BehaviorType == ModuleBehaviorType.Executor)
                    {
                        _log.Info("User [{0}] executed [{1}] in [{2}]", usrMsg.Author, usrMsg.Content, behavior.GetType().Name);
                    }
                    return;
                }
            }

            var execEnd = Environment.TickCount - execTime;

            string messageContent = usrMsg.Content;
            foreach (var exec in _inputTransformers)
            {
                string newContent;
                if ((newContent = await exec.TransformInput(guild, usrMsg.Channel, usrMsg.Author, messageContent).ConfigureAwait(false)) != messageContent.ToLowerInvariant())
                {
                    messageContent = newContent;
                    break;
                }
            }

            var prefix = DefaultPrefix;
            var isPrefixCommand = messageContent.StartsWith(".prefix", StringComparison.CurrentCultureIgnoreCase);
            if (messageContent.StartsWith(prefix, StringComparison.InvariantCulture) || isPrefixCommand)
            {
                var (Success, Error, Info) = await ExecuteCommandAsync(new CommandContext(_client, usrMsg), messageContent, isPrefixCommand ? 1 : prefix.Length, _services, MultiMatchHandling.Best).ConfigureAwait(false);
                execTime = Environment.TickCount - execTime;

                if (Success)
                {
                    await LogSuccessfulExecution(usrMsg, channel as ITextChannel, execEnd, execTime).ConfigureAwait(false);
                    await CommandExecuted(usrMsg, Info).ConfigureAwait(false);
                    return;
                }
                else if (Error != null)
                {
                    LogErroredExecution(Error, usrMsg, channel as ITextChannel, execEnd, execTime);
                    if (guild != null)
                        await CommandErrored(Info, channel as ITextChannel, Error).ConfigureAwait(false);
                }
            }
            else
            {
                await OnMessageNoTrigger(usrMsg).ConfigureAwait(false);
            }

            foreach (var exec in _lateExecutors)
            {
                await exec.LateExecute(_client, guild, usrMsg).ConfigureAwait(false);
            }
        }

        public Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context, string input, int argPos, IServiceProvider servicesProvider, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
            => ExecuteCommand(context, input.Substring(argPos), servicesProvider, multiMatchHandling);

        public async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(CommandContext context, string input, IServiceProvider services, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
        {
            var searchResult = _commandService.Search(context, input);
            if (!searchResult.IsSuccess)
                return (false, null, null);

            var commands = searchResult.Commands;
            var preconditionResults = new Dictionary<CommandMatch, PreconditionResult>();

            foreach (var match in commands)
            {
                preconditionResults[match] = await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);
            }

            var sucessfulPreconditions = preconditionResults
                .Where(x => x.Value.IsSuccess)
                .ToArray();

            if (sucessfulPreconditions.Length == 0)
            {
                var bestCandidate = preconditionResults
                    .OrderByDescending(x => x.Key.Command.Priority)
                    .FirstOrDefault(x => !x.Value.IsSuccess);
                return (false, bestCandidate.Value.ErrorReason, commands[0].Command);
            }
            
            var parseResultDict = new Dictionary<CommandMatch, ParseResult>();
            foreach (var pair in sucessfulPreconditions)
            {
                var parseResult = await pair.Key.ParseAsync(context, searchResult, pair.Value, services).ConfigureAwait(false);

                if (parseResult.Error == CommandError.MultipleMatches)
                {
                    IReadOnlyList<TypeReaderValue> argList, paramList;
                    switch (multiMatchHandling)
                    {
                        case MultiMatchHandling.Best:
                            argList = parseResult.ArgValues.Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                            paramList = parseResult.ParamValues.Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                            parseResult = ParseResult.FromSuccess(argList, paramList);
                            break;
                    }
                }

                parseResultDict[pair.Key] = parseResult;
            }

            float CalculateScore(CommandMatch match, ParseResult parseResult)
            {
                float argValuesScore = 0, paramValuesScore = 0;

                if (match.Command.Parameters.Count > 0)
                {
                    var argValuesSum = parseResult.ArgValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;
                    var paramValuesSum = parseResult.ParamValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;

                    argValuesScore = argValuesSum / match.Command.Parameters.Count;
                    paramValuesScore = paramValuesSum / match.Command.Parameters.Count;
                }

                var totalArgsScore = (argValuesScore + paramValuesScore) / 2;
                return match.Command.Priority + totalArgsScore * 0.99f;
            }

            var parseResults = parseResultDict
                .OrderByDescending(x => CalculateScore(x.Key, x.Value));

            var successfullParses = parseResults
                .Where(x => x.Value.IsSuccess)
                .ToArray();

            if (successfullParses.Length == 0)
            {
                var bestMatch = parseResults
                    .FirstOrDefault(x => !x.Value.IsSuccess);
                return (false, bestMatch.Value.ErrorReason, commands[0].Command);
            }

            var cmd = successfullParses[0].Key.Command;
            var commandName = cmd.Aliases.First();
            foreach (var exec in _lateBlockers)
            {
                if (await exec.TryBlockLate(_client, context.Message, context.Guild, context.Channel, context.User, cmd.Module.GetTopLevelModule().Name, commandName).ConfigureAwait(false))
                {
                    _log.Info("Late blocking User [{0}] Command: [{1}] in [{2}]", context.User, commandName, exec.GetType().Name);
                    return (false, null, cmd);

                }
            }

            var chosenOverload = successfullParses[0];
            var execResult = (ExecuteResult) await chosenOverload.Key.ExecuteAsync(context, chosenOverload.Value, services).ConfigureAwait(false);

            if (execResult.Exception != null && (!(execResult.Exception is HttpException httpException) || httpException.DiscordCode != 50013))
            {
                lock (errorLogLock)
                {
                    var now = DateTime.Now;
                    File.AppendAllText($"./command_errors_{now:yyyy-MM-dd}.txt",
                    $"[{now:HH:mm-yyyy-MM-dd}]" + Environment.NewLine
                                                        + execResult.Exception.ToString() + Environment.NewLine
                                                        + "------" + Environment.NewLine);
                }
            }

            return (true, null, cmd);
        }
        private readonly object errorLogLock = new object();
    }
}