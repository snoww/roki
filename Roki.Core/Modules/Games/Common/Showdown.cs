using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;
using Roki.Modules.Games.Services;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using StackExchange.Redis;
using Image = SixLabors.ImageSharp.Image;

namespace Roki.Modules.Games.Common
{
    public class Showdown
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
        private static readonly string TempDir = Path.GetTempPath();
        private readonly IDatabase _cache;

        private readonly ITextChannel _channel;
        private readonly DiscordSocketClient _client;
        private readonly ICurrencyService _currency;

        private readonly int _generation;

        private readonly string _player1Id;
        private readonly string _player2Id;

        private readonly Dictionary<IEmote, int> _reactionMap = new Dictionary<IEmote, int>
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
            {TimesTen, 0}
        };

        private readonly Dictionary<IUser, PlayerBet> _scores = new Dictionary<IUser, PlayerBet>();
        private readonly ShowdownService _service;
        private IUserMessage _game;

        private Dictionary<string, List<string>> _teams;
        private Bet? _winner;

        public Showdown(ICurrencyService currency, DiscordSocketClient client, ITextChannel channel, int generation,
            ShowdownService service, IRedisCache cache)
        {
            _currency = currency;
            _cache = cache.Redis.GetDatabase();
            _client = client;
            _channel = channel;
            _generation = generation;
            _service = service;
            GameId = $"{_generation}{Guid.NewGuid().ToSubGuid()}";
            _player1Id = Guid.NewGuid().ToSubGuid();
            _player2Id = Guid.NewGuid().ToSubGuid();
        }

        public string GameId { get; }

        public async Task StartGameAsync()
        {
            try
            {
                await InitializeGame().ConfigureAwait(false);
                await RunAiGameAsync(GameId).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Unable to initialize showdown game {Gameid}", GameId);
                await _channel.SendErrorAsync("Something went wrong with the current game. Please try again later.").ConfigureAwait(false);
                return;
            }

            // block until retrieved teams
            while (_teams == null) await Task.Delay(1000);

            var t1 = new List<Image<Rgba32>>();
            var t2 = new List<Image<Rgba32>>();

            for (var i = 0; i < _teams["p1"].Count; i++)
            {
                t1.Add(Image.Load<Rgba32>(GetSprite(_teams["p1"][i])));
                t2.Add(Image.Load<Rgba32>(GetSprite(_teams["p2"][i])));
            }

            t1.Sort((x, y) => y.Width.CompareTo(x.Width));
            t2.Sort((x, y) => y.Width.CompareTo(x.Width));
            await using (MemoryStream stream = GetTeamGif(t1, t2))
            {
                for (var i = 0; i < t1.Count; i++)
                {
                    t1[i].Dispose();
                    t2[i].Dispose();
                }

                EmbedBuilder start = new EmbedBuilder().WithDynamicColor(_channel.GuildId)
                    .WithTitle($"[Gen {_generation}] Random Battle - ID: `{GameId}`")
                    .WithDescription(
                        "A Pokemon battle is about to start!\nAdd reactions below to select your bet. You cannot undo your bets.\ni.e. Adding reactions `P1 10 100` means betting on 110 on P1.")
                    .WithImageUrl($"attachment://pokemon-{GameId}.gif")
                    .AddField("Player 1", string.Join('\n', _teams["p1"]), true)
                    .AddField("Player 2", string.Join('\n', _teams["p2"]), true);

                _game = await _channel.SendFileAsync(stream, $"pokemon-{GameId}.gif", embed: start.Build()).ConfigureAwait(false);
            }

            await ReactionHandler().ConfigureAwait(false);

            if (_scores.Count == 0)
            {
                await _channel.SendErrorAsync("Not enough players to start the bet.\nBet is cancelled").ConfigureAwait(false);
                await _game.RemoveAllReactionsAsync().ConfigureAwait(false);
                return;
            }

            foreach ((IUser key, PlayerBet value) in _scores)
            {
                await _currency
                    .RemoveAsync(key.Id, "BetShowdown Entry", value.Amount * value.Multiple, _channel.Guild.Id, _channel.Id, _game.Id)
                    .ConfigureAwait(false);
            }

            // block until game is finished
            var counter = 0;
            while (_winner == null)
            {
                await Task.Delay(1000);
                // after 60 seconds in addition to the 35 seconds, assume error occured and stop game
                if (counter++ > 60)
                {
                    Logger.Warn("Something went wrong with showdown game {GameId}", GameId);
                    await _channel.SendErrorAsync("Something went wrong with the current game. Please try again later.").ConfigureAwait(false);
                    return;
                }
            }

            var winners = new StringBuilder();
            var losers = new StringBuilder();

            // doing this in two loops instead of one to create two transactions (bet entry and bet payout)
            foreach ((IUser key, PlayerBet value) in _scores)
            {
                long before = GetCurrency(key.Id) + value.Amount * value.Multiple;
                if (_winner != value.Player)
                {
                    losers.AppendLine($"{key.Username} `{before:N0} > {GetCurrency(key.Id):N0}`");
                    continue;
                }

                long won = value.Amount * value.Multiple * 2;
                await _currency.AddAsync(key.Id, "BetShowdown Payout", won, _channel.Guild.Id, _channel.Id, _game.Id).ConfigureAwait(false);
                winners.AppendLine($"{key.Username} won `{won:N0}` {Roki.Properties.CurrencyIcon}\n\t`{before:N0} > {GetCurrency(key.Id):N0}`");
            }

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(_channel.GuildId);
            if (winners.Length > 1)
            {
                embed.WithDescription($"{_winner} has won the battle!\nCongratulations!\n{winners}\n");
                await _channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            if (losers.Length > 1)
            {
                if (winners.Length > 1)
                {
                    await _channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                        .WithDescription($"Better luck next time!\n{losers}\n")).ConfigureAwait(false);
                }
                else
                {
                    await _channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                        .WithDescription($"{_winner} has won the battle!\nBetter luck next time!\n{losers}\n")).ConfigureAwait(false);
                }
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
                    reaction.User.Value.IsBot)
                {
                    return Task.CompletedTask;
                }

                IUser user = reaction.User.Value;
                Task<Task> _ = Task.Run(async () =>
                {
                    // If user doesn't exist in the dictionary yet
                    if (!_scores.ContainsKey(user))
                    {
                        if (Equals(reaction.Emote, Player1))
                        {
                            _scores.Add(user, new PlayerBet {Player = Bet.P1, Amount = 0});
                        }
                        else if (Equals(reaction.Emote, Player2))
                        {
                            _scores.Add(user, new PlayerBet {Player = Bet.P2, Amount = 0});
                        }
                        else
                        {
                            IUserMessage notEnoughMsg = await _channel.SendErrorAsync($"<@{reaction.UserId}> Please select a player to bet on first.")
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
                        long currency = await _currency.GetCurrency(user.Id, _channel.GuildId).ConfigureAwait(false);
                        if (reaction.Emote.Equals(AllIn))
                        {
                            _scores[user].Amount = currency;
                            _scores[user].Multiple = 1;
                            await _game.RemoveReactionsAsync(reaction.User.Value, _reactionMap.Keys.Where(r => !r.Equals(AllIn)).ToArray())
                                .ConfigureAwait(false);
                        }
                        else if (reaction.Emote.Equals(TimesTwo) && currency >= _scores[user].Amount * _scores[user].Multiple * 2)
                        {
                            _scores[user].Multiple *= 2;
                        }
                        else if (reaction.Emote.Equals(TimesFive) && currency >= _scores[user].Amount * _scores[user].Multiple * 5)
                        {
                            _scores[user].Multiple *= 5;
                        }
                        else if (reaction.Emote.Equals(TimesTen) && currency >= _scores[user].Amount * _scores[user].Multiple * 10)
                        {
                            _scores[user].Multiple *= 10;
                        }
                        else if (!reaction.Emote.Equals(TimesTwo) && !reaction.Emote.Equals(TimesFive) && !reaction.Emote.Equals(TimesTen) &&
                                 currency >= _scores[user].Amount + _reactionMap[reaction.Emote] * _scores[user].Multiple)
                        {
                            _scores[user].Amount += _reactionMap[reaction.Emote];
                        }
                        else
                        {
                            IUserMessage notEnoughMsg = await _channel
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
            // put this in solution somewhere later
            string[] roki1 = await File.ReadAllLinesAsync("/home/snow/Documents/showdown/roki.env").ConfigureAwait(false);
            string[] roki2 = await File.ReadAllLinesAsync("/home/snow/Documents/showdown/roki1.env").ConfigureAwait(false);
            for (var i = 0; i < roki1.Length; i++)
            {
                if (roki1[i].StartsWith("PS_USERNAME", StringComparison.Ordinal))
                {
                    roki1[i] = "PS_USERNAME=roki" + _player1Id;
                }
                else if (roki1[i].StartsWith("USER_TO_CHALLENGE", StringComparison.Ordinal))
                {
                    roki1[i] = "USER_TO_CHALLENGE=roki" + _player2Id;
                }
                else if (roki1[i].StartsWith("POKEMON_MODE", StringComparison.Ordinal))
                {
                    roki1[i] = $"POKEMON_MODE=gen{_generation}randombattle";
                    break;
                }
            }

            for (var i = 0; i < roki2.Length; i++)
            {
                if (roki1[i].StartsWith("PS_USERNAME", StringComparison.Ordinal))
                {
                    roki1[i] = "PS_USERNAME=roki" + _player2Id;
                }
                else if (roki1[i].StartsWith("POKEMON_MODE", StringComparison.Ordinal))
                {
                    roki1[i] = $"POKEMON_MODE=gen{_generation}randombattle";
                    break;
                }
            }

            await File.WriteAllTextAsync($"{TempDir}roki-{_player1Id}.env", string.Join('\n', roki1)).ConfigureAwait(false);
            await File.WriteAllTextAsync($"{TempDir}roki-{_player2Id}.env", string.Join('\n', roki2)).ConfigureAwait(false);
        }

        private async Task RunAiGameAsync(string uid)
        {
            using var proc = new Process
            {
                StartInfo =
                {
                    FileName = "bash",
                    Arguments = $"scripts/showdown.sh {TempDir}roki-{_player1Id}.env {TempDir}roki-{_player2Id}.env",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            proc.Start();
            // process in background thread
            ProcessGame(proc.StandardOutput, uid);

            await proc.WaitForExitAsync();
        }

        private void ProcessGame(StreamReader reader, string uid)
        {
            Task _ = Task.Run(async () =>
            {
                var gameIdFound = false;
                var teamsFound = false;
                var winnerFound = false;
                var teams = new Dictionary<string, List<string>>(2);
                while (!reader.EndOfStream)
                {
                    string output = await reader.ReadLineAsync().ConfigureAwait(false);

                    if (teamsFound && gameIdFound && !winnerFound && output!.Contains("Winner:", StringComparison.Ordinal))
                    {
                        winnerFound = true;
                        _winner = output.Contains(_player1Id, StringComparison.Ordinal) ? Bet.P1 : Bet.P2;
                    }

                    if (!teamsFound && output!.Contains("|request|{", StringComparison.Ordinal))
                    {
                        (string p, List<string> team) = ParseTeam(output);
                        teams.Add(p, team);
                        if (teams.Count == 2)
                        {
                            teamsFound = true;
                            _teams = teams;
                        }
                    }
                    else if (!gameIdFound && output!.Contains(">battle-gen", StringComparison.Ordinal))
                    {
                        Match match = Regex.Match(output, ">battle-gen\\drandombattle-(\\d+)");
                        string gameId = match.Groups[1].Value;
                        gameIdFound = true;
                        await _cache.StringSetAsync($"pokemon:{uid}", gameId, TimeSpan.FromMinutes(5), flags: CommandFlags.FireAndForget).ConfigureAwait(false);
                        await File.AppendAllTextAsync(@"./data/pokemon_betshowdown_logs.txt", $"{uid}={gameId}\n");
                    }
                }

                reader.Close();
                File.Delete($"{TempDir}roki-{_player1Id}.env");
                File.Delete($"{TempDir}roki-{_player2Id}.env");
            });
        }

        private static (string, List<string>) ParseTeam(string rawTeam)
        {
            string json = rawTeam.Substring(rawTeam.IndexOf("{", StringComparison.OrdinalIgnoreCase));
            using JsonDocument team = JsonDocument.Parse(json);
            List<string> teamList = team.RootElement.GetProperty("side").GetProperty("pokemon").EnumerateArray().Select(pokemon => pokemon.GetProperty("details").GetString()).ToList();
            string player = team.RootElement.GetProperty("side").GetProperty("id").GetString();
            return (player, teamList);
        }

        private static string GetSprite(string pokemon)
        {
            pokemon = pokemon.Split(',').First().ToLowerInvariant().SanitizeStringFull();
            IEnumerable<string> ani = Directory.EnumerateFiles("./data/pokemon/gen5ani", $"{pokemon.Substring(0, 3)}*", SearchOption.AllDirectories);

            foreach (string path in ani)
            {
                string sprite = path.Split("/").Last().Replace(".gif", "").SanitizeStringFull();
                if (sprite.Equals(pokemon, StringComparison.Ordinal))
                {
                    return path;
                }
            }

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

        private static MemoryStream GetTeamGif(IReadOnlyList<Image<Rgba32>> sprites, IReadOnlyList<Image<Rgba32>> team2)
        {
            var width = 0;
            var width2 = 0;
            var height = 0;
            var height2 = 0;
            var frames = 0;

            for (var i = 0; i < sprites.Count; i++)
            {
                Image<Rgba32> sprite = sprites[i];
                Image<Rgba32> sprite2 = team2[i];

                if (i != sprites.Count - 1)
                {
                    width += (int) Math.Round(sprite.Width * 0.5);
                    width2 += (int) Math.Round(sprite2.Width * 0.6);
                }
                else
                {
                    width += sprite.Width;
                    width2 += sprite2.Width;
                }

                if (sprite.Height > height)
                {
                    // gets the max height of all sprites
                    height = sprite.Height;
                }

                if (sprite2.Height > height2)
                {
                    height2 = sprite2.Height;
                }

                frames += sprite.Frames.Count + sprite2.Frames.Count;
            }

            // average the number of frames
            frames /= sprites.Count + team2.Count;

            // average the width
            width = (width + width2) / 2;

            // setup canvas
            using var image = new Image<Rgba32>(width, height + height2);
            var graphicsOptions = new GraphicsOptions();

            for (var i = 0; i < frames; i++)
            {
                using Image<Rgba32> emptyFrame = image.Frames.CloneFrame(0);
                var currentWidth = 0;
                // int offset = width / teamSize;

                foreach (Image<Rgba32> sprite in sprites)
                {
                    using Image<Rgba32> frame = sprite.Frames.CloneFrame(i % sprite.Frames.Count);
                    var y = 0;
                    if (frame.Height < height)
                    {
                        // midpoint of top frame
                        y = height / 2 - frame.Height / 2;
                    }

                    emptyFrame.Mutate(x => x.DrawImage(frame, new Point(currentWidth, y), graphicsOptions));
                    currentWidth += (int) Math.Round(frame.Width * 0.5);
                }

                currentWidth = 0;

                foreach (Image<Rgba32> sprite in team2)
                {
                    using Image<Rgba32> frame = sprite.Frames.CloneFrame(i % sprite.Frames.Count);
                    var y = 0;
                    if (frame.Height <= height2)
                    {
                        // midpoint of bottom frame
                        y = height2 / 2 + height - frame.Height / 2;
                    }

                    emptyFrame.Mutate(x => x.DrawImage(frame, new Point(currentWidth, y), graphicsOptions));
                    currentWidth += (int) Math.Round(frame.Width * 0.5);
                }

                // IMPORTANT: this makes the gif not ghost
                emptyFrame.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance).DisposalMethod = GifDisposalMethod.RestoreToBackground;
                image.Frames.AddFrame(emptyFrame.Frames.RootFrame);
            }

            image.Frames.RemoveFrame(0);
            var stream = new MemoryStream();
            image.SaveAsGif(stream, new GifEncoder());
            stream.Position = 0;
            return stream;
        }

        private long GetCurrency(ulong userId)
        {
            return _currency.GetCurrency(userId, _channel.GuildId).Result;
        }

        private enum Bet
        {
            P1 = 1,
            P2 = 2
        }

        private class PlayerBet
        {
            public long Amount { get; set; }
            public int Multiple { get; set; } = 1;
            public Bet? Player { get; set; }
        }
    }
}