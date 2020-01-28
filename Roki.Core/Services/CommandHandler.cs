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
using Roki.Extensions;

namespace Roki.Services
{
    public class CommandHandler : IRokiService
    {
        private const float OneThousandth = 1.0f / 1000;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        private readonly DbService _db;
        private readonly object _errorLogLock = new object();

        private readonly Logger _log;

//        private readonly Configuration _config;
        private readonly Roki _roki;
        private readonly IServiceProvider _services;

        public CommandHandler(DiscordSocketClient client,
            CommandService commandService,
            DbService db,
            Roki roki,
            IServiceProvider services)
        {
            _client = client;
            _commandService = commandService;
            _db = db;
            _roki = roki;
            _services = services;

            _log = LogManager.GetCurrentClassLogger();

            DefaultPrefix = Roki.Properties.Prefix;
        }


        public string DefaultPrefix { get; }

        public ConcurrentDictionary<ulong, uint> UserMessagesSent { get; } = new ConcurrentDictionary<ulong, uint>();

        public event Func<IUserMessage, CommandInfo, Task> CommandExecuted = delegate { return Task.CompletedTask; };
        public event Func<CommandInfo, ITextChannel, string, Task> CommandErrored = delegate { return Task.CompletedTask; };
        public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

        public Task StartHandling()
        {
            _client.MessageReceived += message =>
            {
                var _ = Task.Run(() => MessageReceivedHandler(message));
                return Task.CompletedTask;
            };
            
            return Task.CompletedTask;
        }

        private Task LogSuccessfulExecution(IMessage usrMsg, IGuildChannel channel, params int[] execPoints)
        {
            _log.Info("Command Executed after " + string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))) + "s\n\t" +
                      "User: {0}\n\t" +
                      "Server: {1}\n\t" +
                      "Channel: {2}\n\t" +
                      "Message: {3}",
                usrMsg.Author + " [" + usrMsg.Author.Id + "]",
                channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]",
                channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]",
                usrMsg.Content
            );
            
            return Task.CompletedTask;
        }

        private void LogErroredExecution(string errorMessage, IMessage usrMsg, IGuildChannel channel, params int[] execPoints)
        {
            _log.Warn("Command Errored after " + string.Join("/", execPoints.Select(x => (x * OneThousandth).ToString("F3"))) + "s\n\t" +
                      "User: {0}\n\t" +
                      "Server: {1}\n\t" +
                      "Channel: {2}\n\t" +
                      "Message: {3}\n\t" +
                      "Error: {4}",
                usrMsg.Author + " [" + usrMsg.Author.Id + "]",
                channel == null ? "PRIVATE" : channel.Guild.Name + " [" + channel.Guild.Id + "]",
                channel == null ? "PRIVATE" : channel.Name + " [" + channel.Id + "]",
                usrMsg.Content,
                errorMessage
            );
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
                _log.Warn("Error in Command Handler");
                _log.Warn(e);
                if (e.InnerException != null)
                {
                    _log.Warn("Inner Exception");
                    _log.Warn(e.InnerException);
                }
            }
        }

        private async Task TryRunCommand(SocketGuild guild, ISocketMessageChannel channel, IUserMessage userMessage)
        {
            var execTime = Environment.TickCount;

            var execPoint = Environment.TickCount - execTime;

            var messageContent = userMessage.Content;

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
        }

        private Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(ICommandContext context, string input, int argPos,
            IServiceProvider servicesProvider, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
        {
            return ExecuteCommand(context, input.Substring(argPos), servicesProvider, multiMatchHandling);
        }

        private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommand(ICommandContext context, string input,
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
            foreach (var (commandMatch, preconditionResult) in successfulPreconditions)
            {
                var parseResult = await commandMatch.ParseAsync(context, searchResult, preconditionResult, services).ConfigureAwait(false);

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

                parseResultDict[commandMatch] = parseResult;
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
            var (key, value) = successfulParses[0];
            var execResult = (ExecuteResult) await key.ExecuteAsync(context, value, services).ConfigureAwait(false);

            if (execResult.Exception != null && (!(execResult.Exception is HttpException httpException) || httpException.DiscordCode != 50013))
                lock (_errorLogLock)
                {
                    var now = DateTime.Now;
                    File.AppendAllText($"./command_errors_{now:yyyy-MM-dd}.txt",
                        $"[{now:HH:mm-yyyy-MM-dd}]\n{execResult.Exception}");
                }

            return (true, null, cmd);
        }
    }
}