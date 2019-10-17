using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using NLog;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Games.Services;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class ShowdownCommands : RokiSubmodule<ShowdownService>
        {
            private readonly DiscordSocketClient _client;
            private readonly ICurrencyService _currency;
            private const string Gen7SpriteUrl = "http://play.pokemonshowdown.com/sprites/xydex/";
            private const string Gen6SpriteUrl = "http://play.pokemonshowdown.com/sprites/xy/";
            private const string Gen5SpriteUrl = "http://play.pokemonshowdown.com/sprites/bw/";
            private const string Gen4SpriteUrl = "http://play.pokemonshowdown.com/sprites/dpp/";
            private static readonly Emote Player1 = Emote.Parse("<:p1:633334846386601984>");
            private static readonly Emote Player2 = Emote.Parse("<:p2:633334846441127936>");
            private static readonly Emote One = Emote.Parse("<:1_:633332839454343199>");
            private static readonly Emote Five = Emote.Parse("<:5_:633332839588298756>");
            private static readonly Emote Ten = Emote.Parse("<:10:633332839391428609>");
            private static readonly Emote Twenty = Emote.Parse("<:20:633332839584366602>");
            private static readonly Emote Fifty = Emote.Parse("<:50:633332839709933591>");
            private static readonly Emote Hundred = Emote.Parse("<:100:633332839605338162>");
            private static readonly Emote TwoHundredFifty = Emote.Parse("<:250:633332839601143848>");
            private static readonly Emote FiveHundred = Emote.Parse("<:500:633332839806664704>");
            private readonly IEmote[] _reactionPlayer = 
            {
                Player1,
                Player2,
            };
            private readonly IEmote[] _reactionBet = 
            {
                One,
                Five,
                Ten,
                Twenty,
                Fifty,
                Hundred,
                TwoHundredFifty,
                FiveHundred
            };
            private readonly Dictionary<IEmote, long> _betMap = new Dictionary<IEmote, long>
            {
                {One, 1},
                {Five, 5},
                {Ten, 10},
                {Twenty, 20},
                {Fifty, 50},
                {Hundred, 100},
                {TwoHundredFifty, 250},
                {FiveHundred, 500},
            };
            
            public ShowdownCommands(DiscordSocketClient client, ICurrencyService currency)
            {
                _client = client;
                _currency = currency;
            }
            
            private enum BetPlayer
            {
                PLAYER1 = 1,
                P1 = 1,
                PLAYER2 = 2,
                P2 = 2,
            }

            private class PlayerBet
            {
                public long Amount { get; set; }
                public BetPlayer? Bet { get; set; }
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task BetPokemonGame(int gen = 7)
            {
                if (_service.Games.TryGetValue(ctx.Channel.Id, out _))
                {
                    await ctx.Channel.SendErrorAsync("Game already in progress in current channel.");
                    return;
                }

                string generation;
//                if (gen == 6)
//                    generation = "6";
//                else 
                if (gen == 5)
                    generation = "5";
                else if (gen == 4)
                    generation = "4";
                else
                    generation = "7";
                
                _service.Games.TryAdd(ctx.Channel.Id, $"{ctx.User.Username}'s game");
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

                var (gameText, uid) = await _service.StartAiGameAsync(generation).ConfigureAwait(false);
                var index = gameText.IndexOf("|start", StringComparison.Ordinal);
                var gameIntro = gameText.Substring(0, index);
                var gameTurns = gameText.Substring(index + 1);

                var intro = _service.ParseIntro(gameIntro);
                
                var t1 = new List<Image<Rgba32>>();
                var t2 = new List<Image<Rgba32>>();

                IUserMessage startMsg;
                try
                {
                    for (int i = 0; i < intro[0].Count; i++)
                    {
                        t1.Add(GetPokemonImage(intro[0][i], generation));
                        t2.Add(GetPokemonImage(intro[1][i], generation));
                    }
                
                    using (var bitmap1 = t1.MergePokemonTeam())
                    using (var bitmap2 = t2.MergePokemonTeam())
                    using (var bitmap = bitmap1.MergeTwoVertical(bitmap2, out var format))
                    using (var ms = bitmap.ToStream(format))
                    {
                        for (int i = 0; i < t1.Count; i++)
                        {
                            t1[i].Dispose();
                            t2[i].Dispose();
                        }
                    
                        var start = new EmbedBuilder().WithOkColor()
                            .WithTitle($"[Gen {generation}] Random Battle - ID: `{uid}`")
                            .WithDescription("A Pokemon battle is about to start!\nAdd reactions below to select your bet. You cannot undo your bets.\ni.e. Adding reactions `P1 10 20 100` means betting on 130 on P1.")
                            .WithImageUrl($"attachment://pokemon.{format.FileExtensions.First()}")
                            .AddField("Player 1", string.Join('\n', intro[0]), true)
                            .AddField("Player 2", string.Join('\n', intro[1]), true);

                        startMsg = await ctx.Channel.SendFileAsync(ms, $"pokemon.{format.FileExtensions.First()}", embed: start.Build()).ConfigureAwait(false);
                        await startMsg.AddReactionsAsync(_reactionPlayer).ConfigureAwait(false);
                        await startMsg.AddReactionsAsync(_reactionBet).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    _log.Warn(e);
                    _log.Info(gameIntro);
                    await ctx.Channel.SendErrorAsync("Unable to start game, please try again.\nplease @snow about this issue.");
                    _service.Games.TryRemove(ctx.Channel.Id, out _);
                    return;
                }

//                var joinedPlayers = new Dictionary<IUser, PlayerBet>();
                var joinedReactions = new Dictionary<IUser, PlayerBet>();
                var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                _client.ReactionAdded += (cachedMessage, channel, reaction) =>
                {
                    if (ctx.Channel.Id != channel.Id || cachedMessage.Value.Id != startMsg.Id || !_reactionPlayer.Contains(reaction.Emote) && !_reactionBet.Contains(reaction.Emote) ||
                        DateTime.UtcNow > timeout || reaction.User.Value.IsBot) return Task.CompletedTask;
                    var user = reaction.User.Value;
                    var _ = Task.Run(async () =>
                    {
                        if (!joinedReactions.ContainsKey(user))
                        {
                            if (Equals(reaction.Emote, Player1))
                                joinedReactions.Add(user, new PlayerBet{Bet = BetPlayer.P1, Amount = 0});
                            else if (Equals(reaction.Emote, Player2))
                                joinedReactions.Add(user, new PlayerBet{Bet = BetPlayer.P2, Amount = 0});
                            else
                            {
                                var notEnoughMsg = await ctx.Channel.SendErrorAsync($"<@{reaction.UserId}> Please select a player to bet on first.").ConfigureAwait(false);
                                await startMsg.RemoveReactionAsync(reaction.Emote, reaction.User.Value, RequestOptions.Default).ConfigureAwait(false);
                                notEnoughMsg.DeleteAfter(3);
                            }

                            return Task.CompletedTask;
                        }
                        if (joinedReactions[user].Amount >= 0)
                        {
                            if (_currency.GetCurrency(user.Id) >= joinedReactions[user].Amount + _betMap[reaction.Emote])
                                joinedReactions[user].Amount += _betMap[reaction.Emote];
                            else
                            {
                                var notEnoughMsg = await ctx.Channel.SendErrorAsync($"<@{reaction.User.Value.Id}> You do not have enough currency to make that bet.").ConfigureAwait(false);
                                await startMsg.RemoveReactionAsync(reaction.Emote, reaction.User.Value, RequestOptions.Default).ConfigureAwait(false);
                                notEnoughMsg.DeleteAfter(5);
                            }
                            return Task.CompletedTask;
                        }

                        joinedReactions[user].Bet = Equals(Player1, reaction.Emote) ? BetPlayer.P1 : BetPlayer.P2;
                        return Task.CompletedTask;
                    });
                    
                    return Task.CompletedTask;
                };

                Thread.Sleep(TimeSpan.FromSeconds(35));
                if (joinedReactions.Count == 0)
                {
                    await ctx.Channel.SendErrorAsync("Not enough players to start the bet.\nBet is cancelled");
                    await startMsg.RemoveAllReactionsAsync().ConfigureAwait(false);
                    _service.Games.TryRemove(ctx.Channel.Id, out _);
                    return;
                }

                foreach (var (key, value) in joinedReactions)
                {
                    await _currency
                        .ChangeAsync(key, "BetShowdown Entry", -value.Amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id,
                            ctx.Message.Id)
                        .ConfigureAwait(false);
                }
                
                /*_client.MessageReceived += message =>
                {
                    if (ctx.Channel.Id != message.Channel.Id)
                        return Task.CompletedTask;

                    var _ = Task.Run(async () =>
                    {
                        var args = message.Content.Split();
                        if (!string.Equals(args[0], "join", StringComparison.OrdinalIgnoreCase) || !long.TryParse(args[1], out var amount) ||
                            !Enum.TryParse<BetPlayer>(args[2].ToUpperInvariant(), out var betPlayer) || DateTime.UtcNow >= timeout) return Task.CompletedTask;
                        if (amount <= 0)
                        {
                            await ctx.Channel.SendErrorAsync("The minimum bet for this game is 1 stone").ConfigureAwait(false);
                            return Task.CompletedTask;
                        }

                        var removed = await _currency
                            .ChangeAsync(message.Author, "BetShowdown Entry", -amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id,
                                ctx.Message.Id)
                            .ConfigureAwait(false);
                        if (!removed)
                        {
                            await ctx.Channel.SendErrorAsync("Not enough stones.").ConfigureAwait(false);
                            return Task.CompletedTask;
                        }

                        var added = joinedPlayers.TryAdd(message.Author, new PlayerBet
                        {
                            Amount = amount,
                            Bet = betPlayer
                        });
                        if (added)
                        {
                            var joinedMsg = await ctx.Channel
                                .EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"{message.Author.Mention} has joined the bet."))
                                .ConfigureAwait(false);
                            joinedMsg.DeleteAfter(5);
                            await message.DeleteAsync().ConfigureAwait(false);
                            return Task.CompletedTask;
                        }
                        var alreadyJoinedMsg = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithDescription("You are already in the bet."))
                            .ConfigureAwait(false);
                        alreadyJoinedMsg.DeleteAfter(5);
                        return Task.CompletedTask;
                    });
                    return Task.CompletedTask;
                };

                Thread.Sleep(TimeSpan.FromSeconds(35));
                if (joinedPlayers.Count == 0)
                {
                    await ctx.Channel.SendErrorAsync("Not enough players to start the bet.\nBet is cancelled");
                    _service.Games.TryRemove(ctx.Channel.Id, out _);
                    return;
                }*/
                
                var win = ShowdownService.GetWinner(gameTurns);
                var result = win == "1" ? BetPlayer.P1 : BetPlayer.P2;

                var winners = "";
                var losers = "";

                foreach (var (key, value) in joinedReactions)
                {
                    if (result != value.Bet)
                    {
                        losers += $"{key.Username}\n";
                        continue;
                    }
                    var won = value.Amount * 2;
                    winners += $"{key.Username} won {won} stones\n";
                    await _currency.ChangeAsync(key, "BetShowdown Payout", won, "Server", ctx.User.Id.ToString(), ctx.Guild.Id,
                        ctx.Channel.Id, ctx.Message.Id);
                }
                
                var embed = new EmbedBuilder().WithOkColor();
                if (winners.Length > 1)
                {
                    embed.WithDescription($"Player {win} has won the battle!\nCongratulations!\n{winners}\n");
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                if (losers.Length > 1)
                {
                    if (winners.Length > 1)
                        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                            .WithDescription($"Better luck next time!\n{losers}\n")).ConfigureAwait(false);
                    else 
                        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                            .WithDescription($"Player {win} has won the battle!\nBetter luck next time!\n{losers}\n")).ConfigureAwait(false);
                }
                await startMsg.RemoveReactionsAsync(ctx.Client.CurrentUser, _reactionPlayer).ConfigureAwait(false);
                await startMsg.RemoveReactionsAsync(ctx.Client.CurrentUser, _reactionBet).ConfigureAwait(false);
                _service.Games.TryRemove(ctx.Channel.Id, out _);
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task BetPokemonLog(string uid)
            {
                uid = uid.SanitizeStringFull();
                if (uid.Length != 9)
                {
                    await ctx.Channel.SendErrorAsync("Invalid Game ID").ConfigureAwait(false);
                    return;
                }
                var game = await _service.LoadSavedGameAsync(uid).ConfigureAwait(false);
                if (game == null)
                {
                    await ctx.Channel.SendErrorAsync("Game not found").ConfigureAwait(false);
                    return;
                }
                
                var index = game.IndexOf("|start", StringComparison.Ordinal);
                var generation = uid.Substring(0, 1);
                var gameIntro = game.Substring(0, index);
                var gameTurns = game.Substring(index + 1);
                var intro = _service.ParseIntro(gameIntro);
                var t1 = new List<Image<Rgba32>>();
                var t2 = new List<Image<Rgba32>>();

                ctx.Message.DeleteAfter(5);
                IDMChannel dm;
                try
                {
                    dm = await ctx.User.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                }
                catch 
                {
                    await ctx.Channel.SendErrorAsync("Unable to send DM message. Please try again.").ConfigureAwait(false);
                    return;
                }

                for (int i = 0; i < intro[0].Count; i++)
                {
                    t1.Add(GetPokemonImage(intro[0][i], generation));
                    t2.Add(GetPokemonImage(intro[1][i], generation));
                }
                
                using (var bitmap1 = t1.MergePokemonTeam())
                using (var bitmap2 = t2.MergePokemonTeam())
                using (var bitmap = bitmap1.MergeTwoVertical(bitmap2, out var format))
                using (var ms = bitmap.ToStream(format))
                {
                    for (int i = 0; i < t1.Count; i++)
                    {
                        t1[i].Dispose();
                        t2[i].Dispose();
                    }

                    var startEmbed = new EmbedBuilder().WithOkColor()
                        .WithTitle($"[Gen {generation}] Random Battle Replay - ID: `{uid}`")
                        .WithImageUrl($"attachment://pokemon.{format.FileExtensions.First()}")
                        .AddField("Player 1", string.Join('\n', intro[0]), true)
                        .AddField("Player 2", string.Join('\n', intro[1]), true);
                    await dm.SendFileAsync(ms, $"pokemon.{format.FileExtensions.First()}", embed: startEmbed.Build()).ConfigureAwait(false);
                }

                var turns = _service.ParseTurns(gameTurns);

                await dm.SendPaginatedDmAsync(_client, 0, TurnFunc, turns.Count, 1);

                EmbedBuilder TurnFunc(int turnNum)
                {
                    var turn = turns[turnNum];
                    return new EmbedBuilder().WithOkColor()
                        .WithAuthor($"[Gen {generation}] Random Battle Replay - ID: {uid}")
                        .WithTitle($"Turn: {turnNum}")
                        .WithDescription(turn)
                        .WithFooter($"Turn {turnNum}/{turns.Count}");
                }
            }
            
            private Image<Rgba32> GetPokemonImage(string pokemon, string generation)
            {
                var sprite = _service.GetPokemonSprite(pokemon);
                var wc = new WebClient();
                string genUrl;
                if (generation == "7")
                    genUrl = Gen7SpriteUrl;
                else if (generation == "6")
                    genUrl = Gen6SpriteUrl;
                else if (generation == "5")
                    genUrl = Gen5SpriteUrl;
                else
                    genUrl = Gen4SpriteUrl;
                using (var stream = new MemoryStream(wc.DownloadData(genUrl + sprite + ".png")))
                {
                    return Image.Load(stream.ToArray());
                }
            }
        }
    }
}