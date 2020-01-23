using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Games.Services;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Roki.Modules.Games.Common
{
    public class Showdown
    {
        private readonly ICurrencyService _currency;
        private readonly DbService _db;
        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        
        private readonly ITextChannel _channel;

        private readonly int _generation;
        public string GameId { get; }
        
        private List<List<string>> _teams;
        private IUserMessage _game;

        private readonly Dictionary<IUser, PlayerBet> _scores = new Dictionary<IUser, PlayerBet>();
        
        private static readonly Emote Player1 = Emote.Parse("<:p1:633334846386601984>");
        private static readonly Emote Player2 = Emote.Parse("<:p2:633334846441127936>");
        private static readonly Emote AllIn = Emote.Parse("<:allin:634555484703162369>");
        private static readonly Emote One = Emote.Parse("<:1_:633332839454343199>");
        private static readonly Emote Five = Emote.Parse("<:5_:633332839588298756>");
        private static readonly Emote Ten = Emote.Parse("<:10:633332839391428609>");
        private static readonly Emote Hundred = Emote.Parse("<:100:633332839605338162>");
        private static readonly Emote FiveHundred = Emote.Parse("<:500:633332839806664704>");
        private static readonly Emote TimesTwo = Emote.Parse("<:2x:637062546817417244>");
        private static readonly Emote TimesFive = Emote.Parse("<:5x:637062547169869824>");
        private static readonly Emote TimesTen = Emote.Parse("<:10x:637062547169738752>");
        
        private readonly Dictionary<IEmote, long> _reactionMap = new Dictionary<IEmote, long>
        {
            {Player1, 0},
            {Player2, 0},
            {AllIn, 0},
            {One, 1},
            {Five, 5},
            {Ten, 10},
            {Hundred, 100},
            {FiveHundred, 500},
            {TimesTwo, 0},
            {TimesFive, 0},
            {TimesTen, 0},
        };

        public Showdown(ICurrencyService currency, DbService db, DiscordSocketClient client, ITextChannel channel, int generation)
        {
            _currency = currency;
            _db = db;
            _client = client;
            _channel = channel;
            _generation = generation;
            _log = LogManager.GetCurrentClassLogger();
            GameId = $"{_generation}{Guid.NewGuid().ToString().Substring(0, 7)}";
        }

        private enum Bet
        {
            P1 = 1,
            P2 = 2,
        }

        private class PlayerBet
        {
            public long Amount { get; set; }
            public int Multiple { get; set; } = 1;
            public Bet? Player { get; set; }
        }
        
        public async Task StartGameAsync()
        {
            try
            {
                await InitializeGame().ConfigureAwait(false);
                await RunAiGameAsync(GameId).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.Error(e, $"Unable to initialize showdown game '{GameId}'\n{e}");
                await _channel.SendErrorAsync("Something went wrong with the current game. Please try again later.").ConfigureAwait(false);
                return;
            }
            
            var t1 = new List<Image<Rgba32>>();
            var t2 = new List<Image<Rgba32>>();

            for (int i = 0; i < _teams[0].Count; i++)
            {
                t1.Add(Image.Load<Rgba32>(GetSprite(_teams[0][i])));
                t2.Add(Image.Load<Rgba32>(GetSprite(_teams[1][i])));
            }
            
                    
            using var team1 = t1.MergePokemonTeam();
            using var team2 = t2.MergePokemonTeam();
            using var teamImage = team1.MergeTwoVertical(team2);
            await using (var stream = teamImage.ToStream())
            {
                for (int i = 0; i < t1.Count; i++)
                {
                    t1[i].Dispose();
                    t2[i].Dispose();
                }
                    
                var start = new EmbedBuilder().WithOkColor()
                    .WithTitle($"[Gen {_generation}] Random Battle - ID: `{GameId}`")
                    .WithDescription("A Pokemon battle is about to start!\nAdd reactions below to select your bet. You cannot undo your bets.\ni.e. Adding reactions `P1 10 100` means betting on 110 on P1.")
                    .WithImageUrl($"attachment://pokemon-{GameId}.png")
                    .AddField("Player 1", string.Join('\n', _teams[0]), true)
                    .AddField("Player 2", string.Join('\n', _teams[1]), true);

                _game = await _channel.SendFileAsync(stream, $"pokemon-{GameId}.png", embed: start.Build()).ConfigureAwait(false);
            }

            await ReactionHandler().ConfigureAwait(false);
            var fullGameId = await ShowdownService.GetBetPokemonGame(GameId).ConfigureAwait(false);

            if (_scores.Count == 0)
            {
                await _channel.SendErrorAsync("Not enough players to start the bet.\nBet is cancelled").ConfigureAwait(false);
                await _game.RemoveAllReactionsAsync().ConfigureAwait(false);
                await DeleteLogs(fullGameId).ConfigureAwait(false);
                return;
            }

            var winner = await GetWinnerAsync(fullGameId).ConfigureAwait(false);
            if (winner == null)
            {
                // return in case failed to get winner to prevent infinite loop
                throw new Exception($"Failed to get winner for '{GameId}'");
            }
            
            foreach (var (key, value) in _scores)
            {
                await _currency
                    .ChangeAsync(key.Id, _client.CurrentUser.Id, "BetShowdown Entry", -value.Amount * value.Multiple,
                        _channel.Guild.Id, _channel.Id, _game.Id)
                    .ConfigureAwait(false);
            }
            
            var winners = "";
            var losers = "";
            
            // doing this in two loops instead of one to create two transactions (bet entry and bet payout)
            foreach (var (key, value) in _scores)
            {
                var before = GetCurrency(key.Id) + value.Amount * value.Multiple;
                if (winner != value.Player)
                {
                    losers += $"{key.Username} " +
                              $"`{before:N0}` ⇒ `{GetCurrency(key.Id):N0}`\n";
                    continue;
                }
                
                var won = value.Amount * value.Multiple * 2;
                await _currency.ChangeAsync(_client.CurrentUser.Id, key.Id, "BetShowdown Payout", won, 
                    _channel.Guild.Id, _channel.Id, _game.Id);
                winners += $"{key.Username} won `{won:N0}` {Roki.Properties.CurrencyIcon}\n" +
                           $"\t`{before:N0}` ⇒ `{GetCurrency(key.Id):N0}`\n";
            }
            
            var embed = new EmbedBuilder().WithOkColor();
            if (winners.Length > 1)
            {
                embed.WithDescription($"{winner} has won the battle!\nCongratulations!\n{winners}\n");
                await _channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            if (losers.Length > 1)
            {
                if (winners.Length > 1)
                    await _channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                        .WithDescription($"Better luck next time!\n{losers}\n")).ConfigureAwait(false);
                else 
                    await _channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                        .WithDescription($"{winner} has won the battle!\nBetter luck next time!\n{losers}\n")).ConfigureAwait(false);
            }
            
            await _game.RemoveAllReactionsAsync().ConfigureAwait(false); 
        }

        private async Task ReactionHandler()
        {
            await _game.AddReactionsAsync(_reactionMap.Keys.ToArray()).ConfigureAwait(false);
            _client.ReactionAdded += ReactionAddedHandler;

            Task ReactionAddedHandler(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
            {
                if (_channel.Id != channel.Id || cachedMessage.Value.Id != _game.Id || !_reactionMap.ContainsKey(reaction.Emote) ||
                    reaction.User.Value.IsBot) return Task.CompletedTask;
                var user = reaction.User.Value;
                var _ = Task.Run(async () =>
                {
                    // If user doesn't exist in the dictionary yet
                    if (!_scores.ContainsKey(user))
                    {
                        if (Equals(reaction.Emote, Player1))
                            _scores.Add(user, new PlayerBet {Player = Bet.P1, Amount = 0});
                        else if (Equals(reaction.Emote, Player2))
                            _scores.Add(user, new PlayerBet {Player = Bet.P2, Amount = 0});
                        else
                        {
                            var notEnoughMsg = await _channel.SendErrorAsync($"<@{reaction.UserId}> Please select a player to bet on first.")
                                .ConfigureAwait(false);
                            notEnoughMsg.DeleteAfter(3);
                        }

                        await _game.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                        return Task.CompletedTask;
                    }

                    // If user exists in dictionary and reacted with player1 
                    if (reaction.Emote.Equals(Player1))
                    {
                        _scores[user].Player = Bet.P1;
                        await _game.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                        return Task.CompletedTask;
                    }

                    // If user exists in dictionary and reacted with player2 
                    if (reaction.Emote.Equals(Player2))
                    {
                        _scores[user].Player = Bet.P2;
                        await _game.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                        return Task.CompletedTask;
                    }

                    // If user exists in dictionary and reacted with any other emote in the reactionMap
                    if (_reactionMap.ContainsKey(reaction.Emote))
                    {
                        var currency = _currency.GetCurrency(user.Id);
                        if (reaction.Emote.Equals(AllIn))
                        {
                            _scores[user].Amount = currency;
                            _scores[user].Multiple = 1;
                            await _game.RemoveReactionsAsync(reaction.User.Value, _reactionMap.Keys.Where(r => !r.Equals(AllIn)).ToArray())
                                .ConfigureAwait(false);
                        }
                        else if (reaction.Emote.Equals(TimesTwo) && currency >= _scores[user].Amount * _scores[user].Multiple * 2)
                            _scores[user].Multiple *= 2;
                        else if (reaction.Emote.Equals(TimesFive) && currency >= _scores[user].Amount * _scores[user].Multiple * 5)
                            _scores[user].Multiple *= 5;
                        else if (reaction.Emote.Equals(TimesTen) && currency >= _scores[user].Amount * _scores[user].Multiple * 10)
                            _scores[user].Multiple *= 10;
                        else if (!reaction.Emote.Equals(TimesTwo) && !reaction.Emote.Equals(TimesFive) && !reaction.Emote.Equals(TimesTen) &&
                                 currency >= _scores[user].Amount + _reactionMap[reaction.Emote] * _scores[user].Multiple)
                            _scores[user].Amount += _reactionMap[reaction.Emote];
                        else
                        {
                            var notEnoughMsg = await _channel
                                .SendErrorAsync(
                                    $"<@{reaction.User.Value.Id}> You do not have enough {Roki.Properties.CurrencyIcon} to make that bet.")
                                .ConfigureAwait(false);
                            await _game.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                            notEnoughMsg.DeleteAfter(5);
                        }

                        return Task.CompletedTask;
                    }

                    await _game.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                    return Task.CompletedTask;
                });

                return Task.CompletedTask;
            }

            await Task.Delay(TimeSpan.FromSeconds(35)).ConfigureAwait(false);
            _client.ReactionAdded -= ReactionAddedHandler;
        }
        
        private async Task InitializeGame()
        {
            var env = await File.ReadAllTextAsync("/home/snow/Documents/showdown/.env").ConfigureAwait(false);
            var env2 = await File.ReadAllTextAsync("/home/snow/Documents/showdown2/.env").ConfigureAwait(false);
            env = Regex.Replace(env, @"gen\d", $"gen{_generation}");
            env2 = Regex.Replace(env2, @"gen\d", $"gen{_generation}");
            await File.WriteAllTextAsync("/home/snow/Documents/showdown/.env", env).ConfigureAwait(false);
            await File.WriteAllTextAsync("/home/snow/Documents/showdown2/.env", env2).ConfigureAwait(false);
        }
        
        private async Task RunAiGameAsync(string uid)
        {
            using var proc = new Process {StartInfo = {FileName = "./scripts/ai.sh", UseShellExecute = false, RedirectStandardOutput = true}};
            proc.Start();
            var reader = proc.StandardOutput;
            var gameId = "";
            var teams = new List<List<string>>();
            var gameIdReceived = false;
            while (teams.Count < 2)
            {
                var output = await reader.ReadLineAsync().ConfigureAwait(false);
                if (output.StartsWith("|request|{", StringComparison.OrdinalIgnoreCase))
                {
                    var (p, team) = ParseTeam(output);
                    if (p == "p1") 
                        teams.Insert(0, team);
                    else 
                        teams.Add(team);
                }
                else if (!gameIdReceived && output.Contains("battle-gen", StringComparison.OrdinalIgnoreCase))
                {
                    gameId = output.Substring(output.IndexOf("battle-gen", StringComparison.OrdinalIgnoreCase), 34);
                    gameIdReceived = true;
                }
            }
            proc.WaitForExit();
            File.AppendAllText(@"./data/pokemon-logs/battle-logs", $"{uid}={gameId}\n");
            _teams = teams;
        }
        
        private static (string, List<string>) ParseTeam(string rawTeam)
        {
            var json = rawTeam.Substring(rawTeam.IndexOf("{", StringComparison.OrdinalIgnoreCase));
            using var team = JsonDocument.Parse(json);
            var teamList = team.RootElement.GetProperty("side").GetProperty("pokemon").EnumerateArray().Select(pokemon => pokemon.GetProperty("details").GetString()).ToList();
            var player = team.RootElement.GetProperty("side").GetProperty("id").GetString();
            return (player, teamList);
        }
        
        private static async Task<Bet?> GetWinnerAsync(string gameId)
        {
            var p1 = await File.ReadAllLinesAsync($@"./logs/1-{gameId}.log").ConfigureAwait(false);
            int count = 0;
            while (!p1[^6..^1].Any(l => l.Contains("W: 1", StringComparison.OrdinalIgnoreCase) || l.Contains("W: 0", StringComparison.OrdinalIgnoreCase)))
            {
                if (++count > 30)
                {
                    break;
                }
                
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                p1 = await File.ReadAllLinesAsync($@"./logs/1-{gameId}.log").ConfigureAwait(false);
            }

            if (count > 30)
            {
                return null;
            }
            
            return p1.Any(l => l.Contains("W: 0", StringComparison.OrdinalIgnoreCase)) ? Bet.P2 : Bet.P1;
        }

        private Task DeleteLogs(string gameId)
        {
            try
            {
                var _ = Task.Run(async () =>
                {
                    await Task.Delay(10000).ConfigureAwait(false);
                    File.Delete($@"./logs/rokibot-{gameId}.log");
                });
            }
            catch (Exception e)
            {
                _log.Error(e, $"Could not delete log file for: '{GameId}'");
            }
            
            return Task.CompletedTask;
        }
        
        private static string GetSprite(string pokemon)
        {
            pokemon = pokemon.Split(',').First().ToLowerInvariant().SanitizeStringFull();
            IEnumerable<string> sprites = Directory.EnumerateFiles("./data/pokemon/gen5", $"{pokemon.Substring(0, 3)}*", SearchOption.AllDirectories);

            foreach (string path in sprites)
            {
                string sprite = path.Split("/").Last().Replace(".png", "").SanitizeStringFull();
                if (sprite.Equals(pokemon, StringComparison.Ordinal))
                {
                    return path;
                }
            }

            return null;
        }

        private long GetCurrency(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.Users.GetUserCurrency(userId);
        }
    }
}