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
using Roki.Core.Services.Database.Models;
using Roki.Extensions;

namespace Roki.Core.Services
{
    public class GuildUserComparer : IEqualityComparer<IGuildUser>
    {
        public bool Equals(IGuildUser x, IGuildUser y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(IGuildUser obj)
        {
            return obj.Id.GetHashCode();
        }
    }

    public class CommandHandler : IRService
    {
        private const float OneThousandth = 1.0f / 1000;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        private readonly DbService _db;
        private readonly object _errorLogLock = new object();

        private readonly Logger _log;

//        private readonly Configuration _config;
        private readonly Roki _roki;
        private IEnumerable<IEarlyBehavior> _earlyBehaviors;
        private IEnumerable<IInputTransformer> _inputTransformers;
        private IEnumerable<ILateBlocker> _lateBlockers;
        private IEnumerable<ILateExecutor> _lateExecutors;
        private readonly IServiceProvider _services;

        public CommandHandler(DiscordSocketClient client,
            CommandService commandService,
            DbService db,
//            Configuration config,
            Roki roki,
            IServiceProvider services)
        {
            _client = client;
            _commandService = commandService;
            _db = db;
//            _config = config;
            _roki = roki;
            _services = services;

            _log = LogManager.GetCurrentClassLogger();

            DefaultPrefix = ".";
        }


        public string DefaultPrefix { get; set; }

        public ConcurrentDictionary<ulong, uint> UserMessagesSent { get; } = new ConcurrentDictionary<ulong, uint>();

        public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };
        public event Func<CommandInfo, ITextChannel, string, Task> CommandErrored = delegate { return Task.CompletedTask; };
        public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

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
            _client.MessageReceived += message =>
            {
                var _ = Task.Run(() => MessageReceivedHandler(message));
                return Task.CompletedTask;
            };
            
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
                    await TryRunCommand(guild, channel, msg).ConfigureAwait(false);
                }
                catch
                {
                    //
                }
            }
        }

        private Task LogSuccessfulExecution(IUserMessage usrMsg, ITextChannel channel, params int[] execPoints)
        {
            _log.Info("Command Executed after " + string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))) + "s\n\t" +
                      "User: {0}\n\t" +
                      "Server: {1}\n\t" +
                      "Channel: {2}\n\t" +
                      "Message: {3}",
                usrMsg.Author + " [" + usrMsg.Author.Id + "]", // {0}
                channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]", // {1}
                channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]", // {2}
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
            _log.Warn("Command Errored after " + string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))) + "s\n\t" +
                      "User: {0}\n\t" +
                      "Server: {1}\n\t" +
                      "Channel: {2}\n\t" +
                      "Message: {3}\n\t" +
                      "Error: {4}",
                usrMsg.Author + " [" + usrMsg.Author.Id + "]", // {0}
                channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]", // {1}
                channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]", // {2}
                usrMsg.Content, // {3}
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

        private async Task MessageReceivedHandler(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot || !_roki.Ready.Task.IsCompleted)
                    return;
                if (!(message is SocketUserMessage userMessage))
                    return;

                UserMessagesSent.AddOrUpdate(userMessage.Author.Id, 1, (key, old) => ++old);

                var channel = message.Channel;
                var guild = (message.Channel as SocketTextChannel)?.Guild;

                await TryRunCommand(guild, channel, userMessage).ConfigureAwait(false);
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

        public async Task TryRunCommand(SocketGuild guild, ISocketMessageChannel channel, IUserMessage userMessage)
        {
            var execTime = Environment.TickCount;

            foreach (var behavior in _earlyBehaviors)
                if (await behavior.RunBehavior(_client, guild, userMessage).ConfigureAwait(false))
                {
                    if (behavior.BehaviorType == ModuleBehaviorType.Blocker)
                        _log.Info("Blocked User: [{0}] Message: [{1}] Service: [{2}]", userMessage.Author, userMessage.Content,
                            behavior.GetType().Name);
                    else if (behavior.BehaviorType == ModuleBehaviorType.Executor)
                        _log.Info("User [{0}] executed [{1}] in [{2}]", userMessage.Author, userMessage.Content, behavior.GetType().Name);
                    return;
                }

            var execPoint = Environment.TickCount - execTime;

            var messageContent = userMessage.Content;
            foreach (var exec in _inputTransformers)
            {
                string newContent;
                if ((newContent = await exec.TransformInput(guild, userMessage.Channel, userMessage.Author, messageContent).ConfigureAwait(false)) !=
                    messageContent.ToLowerInvariant())
                {
                    messageContent = newContent;
                    break;
                }
            }

            var prefix = DefaultPrefix;
            var isPrefixCommand = messageContent.StartsWith(".prefix", StringComparison.CurrentCultureIgnoreCase);
            if (messageContent.StartsWith(prefix, StringComparison.InvariantCulture) || isPrefixCommand)
            {
                var (success, error, info) = await ExecuteCommandAsync(new CommandContext(_client, userMessage), messageContent,
                    isPrefixCommand ? 1 : prefix.Length, _services, MultiMatchHandling.Best).ConfigureAwait(false);
                execTime = Environment.TickCount - execTime;

                if (success)
                {
                    await LogSuccessfulExecution(userMessage, channel as ITextChannel, execPoint, execTime).ConfigureAwait(false);
                    await CommandExecuted(userMessage, info).ConfigureAwait(false);
                    return;
                }

                if (error != null)
                {
                    LogErroredExecution(error, userMessage, channel as ITextChannel, execPoint, execTime);
                    if (guild != null)
                        await CommandErrored(info, channel as ITextChannel, error).ConfigureAwait(false);
                }
            }
            else
            {
                await OnMessageNoTrigger(userMessage).ConfigureAwait(false);
            }

            foreach (var exec in _lateExecutors) await exec.LateExecute(_client, guild, userMessage).ConfigureAwait(false);
        }

        private Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(CommandContext context, string input, int argPos,
            IServiceProvider servicesProvider, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
        {
            return ExecuteCommand(context, input.Substring(argPos), servicesProvider, multiMatchHandling);
        }

        private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(CommandContext context, string input,
            IServiceProvider services, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
        {
            var searchResult = _commandService.Search(context, input);
            if (!searchResult.IsSuccess)
                return (false, null, null);

            var commands = searchResult.Commands;
            var preconditionResults = new Dictionary<CommandMatch, PreconditionResult>();

            foreach (var match in commands)
                preconditionResults[match] = await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);

            var successfulPreconditions = preconditionResults
                .Where(x => x.Value.IsSuccess)
                .ToArray();

            if (successfulPreconditions.Length == 0)
            {
                var bestCandidate = preconditionResults
                    .OrderByDescending(x => x.Key.Command.Priority)
                    .FirstOrDefault(x => !x.Value.IsSuccess);
                return (false, bestCandidate.Value.ErrorReason, commands[0].Command);
            }

            var parseResultDict = new Dictionary<CommandMatch, ParseResult>();
            foreach (var pair in successfulPreconditions)
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

            var successfulParses = parseResults
                .Where(x => x.Value.IsSuccess)
                .ToArray();

            if (successfulParses.Length == 0)
            {
                var bestMatch = parseResults
                    .FirstOrDefault(x => !x.Value.IsSuccess);
                return (false, bestMatch.Value.ErrorReason, commands[0].Command);
            }

            var cmd = successfulParses[0].Key.Command;
            var commandName = cmd.Aliases.First();
            foreach (var exec in _lateBlockers)
                if (await exec.TryBlockLate(_client, context.Message, context.Guild, context.Channel, context.User,
                    cmd.Module.GetTopLevelModule().Name, commandName).ConfigureAwait(false))
                {
                    _log.Info("Late blocking User [{0}] Command: [{1}] in [{2}]", context.User, commandName, exec.GetType().Name);
                    return (false, null, cmd);
                }

            var chosenOverload = successfulParses[0];
            var execResult = (ExecuteResult) await chosenOverload.Key.ExecuteAsync(context, chosenOverload.Value, services).ConfigureAwait(false);

            if (execResult.Exception != null && (!(execResult.Exception is HttpException httpException) || httpException.DiscordCode != 50013))
                lock (_errorLogLock)
                {
                    var now = DateTime.Now;
                    File.AppendAllText($"./command_errors_{now:yyyy-MM-dd}.txt",
                        $"[{now:HH:mm-yyyy-MM-dd}]" + Environment.NewLine
                                                    + execResult.Exception + Environment.NewLine
                                                    + "------" + Environment.NewLine);
                }

            return (true, null, cmd);
        }
    }
}