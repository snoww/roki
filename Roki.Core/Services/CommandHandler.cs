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
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        private readonly IConfigurationService _config;

        private readonly IServiceProvider _services;

        public CommandHandler(DiscordSocketClient client, CommandService commandService, IServiceProvider services, IConfigurationService config)
        {
            _client = client;
            _commandService = commandService;
            _services = services;
            _config = config;

            DefaultPrefix = Roki.Properties.Prefix;
        }

        private string DefaultPrefix { get; }

        public event Func<IUserMessage, CommandInfo, Task> CommandOnSuccess = delegate { return Task.CompletedTask; };
        public event Func<ExecuteCommandResult, Task> CommandOnError = delegate { return Task.CompletedTask; };
        public event Func<IUserMessage, Task> OnMessageNoTrigger = delegate { return Task.CompletedTask; };

        public Task StartHandling()
        {
            _client.MessageReceived += message =>
            {
                Task _ = Task.Run(() => MessageReceived(message));
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        }

        private async Task MessageReceived(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot || message is not SocketUserMessage userMessage)
                {
                    return;
                }

                ISocketMessageChannel channel = message.Channel;
                SocketGuild guild = (message.Channel as SocketTextChannel)?.Guild;

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
            if (channel is IDMChannel && message.Content.StartsWith(DefaultPrefix, StringComparison.Ordinal))
            {
                var sw = Stopwatch.StartNew();
                ExecuteCommandResult result = await ExecuteCommandAsync(new CommandContext(_client, message),
                        message.Content[DefaultPrefix.Length..], _services, MultiMatchHandling.Best)
                    .ConfigureAwait(false);

                sw.Stop();
                if (result.Success)
                {
                    await LogSuccess(message, channel as ITextChannel, sw.ElapsedMilliseconds).ConfigureAwait(false);
                    await CommandOnSuccess(message, result.CommandInfo).ConfigureAwait(false);
                    return;
                }

                if (result.Result != null)
                {
                    LogError(result.Result.ErrorReason, message, channel as ITextChannel, sw.ElapsedMilliseconds);
                    await CommandOnError(result).ConfigureAwait(false);
                }

                return;
            }
            
            if (channel is IDMChannel)
            {
                return;
            }

            string prefix = await _config.GetGuildPrefix(guild.Id);
            if (message.Content.StartsWith(prefix, StringComparison.Ordinal))
            {
                var sw = Stopwatch.StartNew();
                ExecuteCommandResult result = await ExecuteCommandAsync(new CommandContext(_client, message),
                        message.Content[prefix.Length..], _services, MultiMatchHandling.Best)
                    .ConfigureAwait(false);

                sw.Stop();
                if (result.Success)
                {
                    await LogSuccess(message, channel as ITextChannel, sw.ElapsedMilliseconds).ConfigureAwait(false);
                    await CommandOnSuccess(message, result.CommandInfo).ConfigureAwait(false);
                    return;
                }

                if (result.Result != null)
                {
                    LogError(result.Result.ErrorReason, message, channel as ITextChannel, sw.ElapsedMilliseconds);
                    await CommandOnError(result).ConfigureAwait(false);
                }
            }
            else
            {
                await OnMessageNoTrigger(message).ConfigureAwait(false);
            }
        }

        private async Task<ExecuteCommandResult> ExecuteCommandAsync(ICommandContext context, string input, IServiceProvider services, MultiMatchHandling multiMatchHandling)
        {
            SearchResult searchResult = _commandService.Search(context, input);
            if (!searchResult.IsSuccess)
            {
                return new ExecuteCommandResult(false, null, null, context);
            }

            IReadOnlyList<CommandMatch> commands = searchResult.Commands;
            var preconditions = new Dictionary<CommandMatch, PreconditionResult>();

            foreach (CommandMatch match in commands)
            {
                preconditions[match] = await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);
            }

            KeyValuePair<CommandMatch, PreconditionResult>[] successes = preconditions.Where(x => x.Value.IsSuccess).ToArray();

            if (successes.Length == 0)
            {
                KeyValuePair<CommandMatch, PreconditionResult> bestMatch = preconditions.OrderByDescending(x => x.Key.Command.Priority).FirstOrDefault(x => !x.Value.IsSuccess);
                return new ExecuteCommandResult(false, bestMatch.Value, commands[0].Command, context);
            }

            var parseResultDict = new Dictionary<CommandMatch, ParseResult>();
            foreach ((CommandMatch match, PreconditionResult precondition) in successes)
            {
                ParseResult parseResult = await match.ParseAsync(context, searchResult, precondition, services).ConfigureAwait(false);

                if (parseResult.Error == CommandError.MultipleMatches)
                {
                    if (multiMatchHandling == MultiMatchHandling.Best)
                    {
                        IReadOnlyList<TypeReaderValue> argList = parseResult.ArgValues
                            .Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                        IReadOnlyList<TypeReaderValue> paramList = parseResult.ParamValues
                            .Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                        parseResult = ParseResult.FromSuccess(argList, paramList);
                    }
                }

                parseResultDict[match] = parseResult;
            }

            static float CalculateScore(CommandMatch match, ParseResult parseResult)
            {
                float argValuesScore = 0, paramValuesScore = 0;

                if (match.Command.Parameters.Count > 0)
                {
                    float argValuesSum = parseResult.ArgValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;
                    float paramValuesSum = parseResult.ParamValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;

                    argValuesScore = argValuesSum / match.Command.Parameters.Count;
                    paramValuesScore = paramValuesSum / match.Command.Parameters.Count;
                }

                float totalArgsScore = (argValuesScore + paramValuesScore) / 2;
                return match.Command.Priority + totalArgsScore * 0.99f;
            }

            KeyValuePair<CommandMatch, ParseResult>[] orderedResults = parseResultDict.OrderByDescending(x => CalculateScore(x.Key, x.Value)).ToArray();
            KeyValuePair<CommandMatch, ParseResult>[] successfulParses = orderedResults.Where(x => x.Value.IsSuccess).ToArray();

            if (successfulParses.Length == 0)
            {
                KeyValuePair<CommandMatch, ParseResult> bestMatch = orderedResults
                    .FirstOrDefault(x => !x.Value.IsSuccess);
                return new ExecuteCommandResult(false, bestMatch.Value, commands[0].Command, context);
            }

            CommandInfo command = successfulParses[0].Key.Command;
            (CommandMatch key, ParseResult value) = successfulParses[0];
            var result = (ExecuteResult) await key.ExecuteAsync(context, value, services).ConfigureAwait(false);

            if (result.Exception != null && result.Exception is not HttpException {DiscordCode: 50013})
            {
                Logger.Error(result.Exception, "Command execute exception");
            }

            return new ExecuteCommandResult(true, null, command, context);
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
                    message.Author, message.Author.Id, channel.Guild.Name, channel.Guild.Id, channel.Name, channel.Id, message.Content
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
                    message.Author, message.Author.Id, channel.Guild.Name, channel.Guild.Id, channel.Name, channel.Id, message.Content, error
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
                    message.Author, message.Author.Id, message.Content, error
                );
            }
        }
    }

    public struct ExecuteCommandResult
    {
        public bool Success { get; }
        public IResult Result { get; }
        public CommandInfo CommandInfo { get; }
        public ICommandContext Context { get; }

        public ExecuteCommandResult(bool success, IResult result, CommandInfo commandInfo, ICommandContext context)
        {
            Success = success;
            Result = result;
            CommandInfo = commandInfo;
            Context = context;
        }
    }
}