using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using NLog;

namespace Roki.Services
{
    public class CommandHandler : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        private readonly IServiceProvider _services;

        private readonly object _errorLock = new object();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string DefaultPrefix { get; }
        
        public event Func<IUserMessage, CommandInfo, Task> CommonOnSuccess = delegate { return Task.CompletedTask; };
        public event Func<CommandInfo, ITextChannel, string, Task> CommonOnError = delegate { return Task.CompletedTask; };
        public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

        public CommandHandler(DiscordSocketClient client, CommandService commandService, IServiceProvider services)
        {
            _client = client;
            _commandService = commandService;
            _services = services;

            DefaultPrefix = Roki.Properties.Prefix;
        }

        public Task StartHandling()
        {
            _client.MessageReceived += message =>
            {
                var _ = Task.Run(() => MessageReceived(message));
                return Task.CompletedTask;
            };
            
            return Task.CompletedTask;
        }

        private async Task MessageReceived(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot || !(message is SocketUserMessage userMessage))
                    return;
                
                var channel = message.Channel;
                var guild = (message.Channel as SocketTextChannel)?.Guild;

                await TryRunCommand(guild, channel, userMessage).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Warn("Error in Command Handler");
                Logger.Warn(e);
                if (e.InnerException != null)
                {
                    Logger.Warn("Inner Exception");
                    Logger.Warn(e.InnerException);
                }
            }
        }

        private async Task TryRunCommand(SocketGuild guild, ISocketMessageChannel channel, IUserMessage message)
        {
            var content = message.Content;

            if (content.StartsWith(DefaultPrefix, StringComparison.InvariantCulture))
            {
                var sw = Stopwatch.StartNew();
                var (success, error, info) = await ExecuteCommandAsync(new CommandContext(_client, message),
                        content.Substring(DefaultPrefix.Length), _services, MultiMatchHandling.Best)
                    .ConfigureAwait(false);

                sw.Stop();
                if (success)
                {
                    await LogSuccess(message, channel as ITextChannel, sw.ElapsedMilliseconds).ConfigureAwait(false);
                    await CommonOnSuccess(message, info).ConfigureAwait(false);
                    return;
                }

                if (error != null)
                {
                    LogError(error, message, channel as ITextChannel, sw.ElapsedMilliseconds);
                    if (guild != null)
                        await CommonOnError(info, channel as ITextChannel, error).ConfigureAwait(false);
                }
            }
            else
            {
                await OnMessageNoTrigger(message).ConfigureAwait(false);
            }
        }

        private async Task<(bool Success, string Error, CommandInfo Info)> ExecuteCommandAsync(ICommandContext context, string input,
            IServiceProvider services, MultiMatchHandling multiMatchHandling)
        {
            var searchResult = _commandService.Search(context, input);
            if (!searchResult.IsSuccess)
                return (false, null, null);

            var commands = searchResult.Commands;
            var preconditions = new Dictionary<CommandMatch, PreconditionResult>();

            foreach (var match in commands)
                preconditions[match] = await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);

            var successes = preconditions.Where(x => x.Value.IsSuccess).ToArray();

            if (successes.Length == 0)
            {
                var bestMatch = preconditions.OrderByDescending(x => x.Key.Command.Priority).FirstOrDefault(x => !x.Value.IsSuccess);
                return (false, bestMatch.Value.ErrorReason, commands[0].Command);
            }

            var parseResultDict = new Dictionary<CommandMatch, ParseResult>();
            foreach (var (match, precondition) in successes)
            {
                var parseResult = await match.ParseAsync(context, searchResult, precondition, services).ConfigureAwait(false);

                if (parseResult.Error == CommandError.MultipleMatches)
                {
                    switch (multiMatchHandling)
                    {
                        case MultiMatchHandling.Best:
                            IReadOnlyList<TypeReaderValue> argList = parseResult.ArgValues
                                .Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                            IReadOnlyList<TypeReaderValue> paramList = parseResult.ParamValues
                                .Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                            parseResult = ParseResult.FromSuccess(argList, paramList);
                            break;
                    }
                }

                parseResultDict[match] = parseResult;
            }

            static float CalculateScore(CommandMatch match, ParseResult parseResult)
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

            var orderedResults = parseResultDict.OrderByDescending(x => CalculateScore(x.Key, x.Value)).ToArray();
            var successfulParses = orderedResults.Where(x => x.Value.IsSuccess).ToArray();

            if (successfulParses.Length == 0)
            {
                var bestMatch = orderedResults
                    .FirstOrDefault(x => !x.Value.IsSuccess);
                return (false, bestMatch.Value.ErrorReason, commands[0].Command);
            }

            var command = successfulParses[0].Key.Command;
            var (key, value) = successfulParses[0];
            var result = (ExecuteResult) await key.ExecuteAsync(context, value, services).ConfigureAwait(false);

            if (result.Exception != null && (!(result.Exception is HttpException httpException) || httpException.DiscordCode != 50013))
                lock (_errorLock)
                {
                    var now = DateTime.Now;
                    File.AppendAllText($"./command_errors_{now:yyyy-MM-dd}.txt",
                        $"[{now:HH:mm-yyyy-MM-dd}]\n{result.Exception}");
                }

            return (true, null, command);
        }
        
        private static Task LogSuccess(IMessage message, IGuildChannel channel, long seconds)
        {
            if (channel != null)
            {
                Logger.Info("Command parsed after " + seconds + " ms\n\t" +
                            "User: {user} [{userid}]\n\t" +
                            "Server: {guild:l} [{guildid}]\n\t" +
                            "Channel: {channel:l} [{channelid}]\n\t" +
                            "Message: {message}",
                    message.Author, message.Author.Id,
                    channel.Guild.Name, channel.Guild.Id,
                    channel.Name, channel.Id,
                    message.Content
                );
            }
            else
            {
                Logger.Info("Command parsed after " + seconds + " ms\n\t" +
                            "User: {user} [{userid}]\n\t" +
                            "Server: PRIVATE\n\t" +
                            "Channel: PRIVATE\n\t" +
                            "Message: {message}",
                    message.Author, message.Author.Id, message.Content
                );
            }
            
            return Task.CompletedTask;
        }

        private static void LogError(string error, IMessage message, IGuildChannel channel, long seconds)
        {
            if (channel != null)
            {
                Logger.Warn("Command parsed after " + seconds + " ms\n\t" +
                            "User: {user} [{userid}]\n\t" +
                            "Server: {guild:l} [{guildid}]\n\t" +
                            "Channel: {channel:l} [{channelid}]\n\t" +
                            "Message: {message}\n\t" +
                            "Error: {error:l}",
                    message.Author, message.Author.Id, 
                    channel.Guild.Name, channel.Guild.Id,
                    channel.Name, channel.Id,
                    message.Content,
                    error
                );
            }
            else
            {
                Logger.Warn("Command parsed after " + seconds + " ms\n\t" +
                            "User: {user} [{userid}]\n\t" +
                            "Server: PRIVATE\n\t" +
                            "Channel: PRIVATE\n\t" +
                            "Message: {message}\n\t" +
                            "Error: {error:l}",
                    message.Author, message.Author.Id,
                    message.Content,
                    error
                );
            }
        }
    }
}