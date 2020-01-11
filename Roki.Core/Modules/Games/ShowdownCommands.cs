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
            private const string Gen5SpriteUrl = "http://play.pokemonshowdown.com/sprites/gen5/";
            private const string Gen4SpriteUrl = "http://play.pokemonshowdown.com/sprites/gen4/";
            private const string Gen3SpriteUrl = "http://play.pokemonshowdown.com/sprites/gen3/";
            private const string Gen2SpriteUrl = "http://play.pokemonshowdown.com/sprites/gen2/";
            private const string Gen1SpriteUrl = "http://play.pokemonshowdown.com/sprites/gen1/";
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
            
            public ShowdownCommands(DiscordSocketClient client, ICurrencyService currency)
            {
                _client = client;
                _currency = currency;
            }
            
            private enum BetPlayer
            {
                P1 = 1,
                P2 = 2,
            }

            private class PlayerBet
            {
                public long Amount { get; set; }
                public int Multiple { get; set; } = 1;
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
                _service.Games.TryAdd(ctx.Channel.Id, "");

                if (gen > 7 || gen < 4) gen = 7;
                
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                var uid = $"{gen}{Guid.NewGuid().ToString().Substring(0, 7)}";
                try
                {
                    await _service.ConfigureAiGameAsync(gen).ConfigureAwait(false);
                    var teams = await _service.RunAiGameAsync(uid).ConfigureAwait(false);

                    var t1 = new List<Image<Rgba32>>();
                    var t2 = new List<Image<Rgba32>>();

                    for (int i = 0; i < teams[0].Count; i++)
                    {
                        t1.Add(GetPokemonImage(teams[0][i], gen));
                        t2.Add(GetPokemonImage(teams[1][i], gen));
                    }

                    using var bitmap1 = t1.MergePokemonTeam();
                    using var bitmap2 = t2.MergePokemonTeam();
                    using var bitmap = bitmap1.MergeTwoVertical(bitmap2, out var format);
                    await using var ms = bitmap.ToStream(format);
                    for (int i = 0; i < t1.Count; i++)
                    {
                        t1[i].Dispose();
                        t2[i].Dispose();
                    }
                    
                    var start = new EmbedBuilder().WithOkColor()
                        .WithTitle($"[Gen {gen}] Random Battle - ID: `{uid}`")
                        .WithDescription("A Pokemon battle is about to start!\nAdd reactions below to select your bet. You cannot undo your bets.\ni.e. Adding reactions `P1 10 100` means betting on 110 on P1.")
                        .WithImageUrl($"attachment://pokemon.{format.FileExtensions.First()}")
                        .AddField("Player 1", string.Join('\n', teams[0]), true)
                        .AddField("Player 2", string.Join('\n', teams[1]), true);

                    var startMsg = await ctx.Channel.SendFileAsync(ms, $"pokemon.{format.FileExtensions.First()}", embed: start.Build()).ConfigureAwait(false);
                    await startMsg.AddReactionsAsync(_reactionMap.Keys.ToArray()).ConfigureAwait(false);

                    var joinedReactions = new Dictionary<IUser, PlayerBet>();
                    _client.ReactionAdded += ReactionAddedHandler;
                    Task ReactionAddedHandler(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
                    {
                        if (ctx.Channel.Id != channel.Id || cachedMessage.Value.Id != startMsg.Id || !_reactionMap.ContainsKey(reaction.Emote) || reaction.User.Value.IsBot) return Task.CompletedTask;
                        var user = reaction.User.Value;
                        var _ = Task.Run(async () =>
                        {
                            // If user doesn't exist in the dictionary yet
                            if (!joinedReactions.ContainsKey(user))
                            {
                                if (Equals(reaction.Emote, Player1))
                                    joinedReactions.Add(user, new PlayerBet{Bet = BetPlayer.P1, Amount = 0});
                                else if (Equals(reaction.Emote, Player2))
                                    joinedReactions.Add(user, new PlayerBet{Bet = BetPlayer.P2, Amount = 0});
                                else
                                {
                                    var notEnoughMsg = await ctx.Channel.SendErrorAsync($"<@{reaction.UserId}> Please select a player to bet on first.").ConfigureAwait(false);
                                    notEnoughMsg.DeleteAfter(3);
                                }
                                await startMsg.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                                return Task.CompletedTask;
                            }
                            // If user exists in dictionary and reacted with player1 
                            if (reaction.Emote.Equals(Player1))
                            {
                                joinedReactions[user].Bet = BetPlayer.P1;
                                await startMsg.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                                return Task.CompletedTask;
                            }
                            
                            // If user exists in dictionary and reacted with player2 
                            if (reaction.Emote.Equals(Player2))
                            {
                                joinedReactions[user].Bet = BetPlayer.P2;
                                await startMsg.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                                return Task.CompletedTask;
                            }
                            // If user exists in dictionary and reacted with any other emote in the reactionMap
                            if (_reactionMap.ContainsKey(reaction.Emote))
                            {
                                var currency = _currency.GetCurrency(user.Id);
                                if (reaction.Emote.Equals(AllIn))
                                {
                                    joinedReactions[user].Amount = currency;
                                    joinedReactions[user].Multiple = 1;
                                    await startMsg.RemoveReactionsAsync(reaction.User.Value, _reactionMap.Keys.Where(r => !r.Equals(AllIn)).ToArray())
                                        .ConfigureAwait(false);
                                }
                                else if (reaction.Emote.Equals(TimesTwo) && currency >= joinedReactions[user].Amount * joinedReactions[user].Multiple * 2)
                                    joinedReactions[user].Multiple *= 2;
                                else if (reaction.Emote.Equals(TimesFive) && currency >= joinedReactions[user].Amount * joinedReactions[user].Multiple * 5)
                                    joinedReactions[user].Multiple *= 5;
                                else if (reaction.Emote.Equals(TimesTen) && currency >= joinedReactions[user].Amount * joinedReactions[user].Multiple * 10)
                                    joinedReactions[user].Multiple *= 10;
                                else if (!reaction.Emote.Equals(TimesTwo) && !reaction.Emote.Equals(TimesFive) && !reaction.Emote.Equals(TimesTen) && 
                                         currency >= joinedReactions[user].Amount + _reactionMap[reaction.Emote] * joinedReactions[user].Multiple) 
                                    joinedReactions[user].Amount += _reactionMap[reaction.Emote];
                                else
                                {
                                    var notEnoughMsg = await ctx.Channel.SendErrorAsync($"<@{reaction.User.Value.Id}> You do not have enough {Roki.Properties.CurrencyIcon} to make that bet.")
                                        .ConfigureAwait(false);
                                    await startMsg.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                                    notEnoughMsg.DeleteAfter(5);
                                }
                                return Task.CompletedTask;
                            }
                            await startMsg.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                            return Task.CompletedTask;
                        });
                        
                        return Task.CompletedTask;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(35)).ConfigureAwait(false);
                    _client.ReactionAdded -= ReactionAddedHandler;
                
                    if (joinedReactions.Count == 0)
                    {
                        await ctx.Channel.SendErrorAsync("Not enough players to start the bet.\nBet is cancelled").ConfigureAwait(false);
                        await startMsg.RemoveAllReactionsAsync().ConfigureAwait(false);
                        _service.Games.TryRemove(ctx.Channel.Id, out _);
                        await Task.Delay(10000).ConfigureAwait(false);
                        await _service.GetWinnerAsync(uid).ConfigureAwait(false);
                        return;
                    }
                    
                    var winner = await _service.GetWinnerAsync(uid).ConfigureAwait(false);
                    var result = winner == 1 ? BetPlayer.P1 : BetPlayer.P2;

                    foreach (var (key, value) in joinedReactions)
                    {
                        await _currency
                            .ChangeAsync(key.Id, ctx.Client.CurrentUser.Id, "BetShowdown Entry", -value.Amount * value.Multiple, ctx.Guild.Id, ctx.Channel.Id,
                                ctx.Message.Id)
                            .ConfigureAwait(false);
                    }

                    var winners = "";
                    var losers = "";

                    foreach (var (key, value) in joinedReactions)
                    {
                        var before = _service.GetCurrency(key.Id) + value.Amount * value.Multiple;
                        if (result != value.Bet)
                        {
                            losers += $"{key.Username} " +
                                      $"`{before:N0}` ⇒ `{_service.GetCurrency(key.Id):N0}`\n";
                            continue;
                        }
                        var won = value.Amount * value.Multiple * 2;
                        await _currency.ChangeAsync(ctx.Client.CurrentUser.Id, key.Id, "BetShowdown Payout", won, ctx.Guild.Id,
                            ctx.Channel.Id, ctx.Message.Id);
                        winners += $"{key.Username} won `{won:N0}` {Roki.Properties.CurrencyIcon}\n" +
                                   $"\t`{before:N0}` ⇒ `{_service.GetCurrency(key.Id):N0}`\n";
                    }
                    
                    var embed = new EmbedBuilder().WithOkColor();
                    if (winners.Length > 1)
                    {
                        embed.WithDescription($"Player {winner} has won the battle!\nCongratulations!\n{winners}\n");
                        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    }
                    if (losers.Length > 1)
                    {
                        if (winners.Length > 1)
                            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                                .WithDescription($"Better luck next time!\n{losers}\n")).ConfigureAwait(false);
                        else 
                            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                                .WithDescription($"Player {winner} has won the battle!\nBetter luck next time!\n{losers}\n")).ConfigureAwait(false);
                    }
                    _service.Games.TryRemove(ctx.Channel.Id, out _);
                    await startMsg.RemoveAllReactionsAsync().ConfigureAwait(false); 
                }
                catch (Exception e)
                {
                    _log.Warn(e);
                    _log.Info(uid);
                    await ctx.Channel.SendErrorAsync("An error occured, please try again.");
                    _service.Games.TryRemove(ctx.Channel.Id, out _);
                }
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task BetPokemonReplay([Leftover] string uid = null)
            {
                uid = uid.SanitizeStringFull();
                if (uid.Length != 8)
                {
                    await ctx.Channel.SendErrorAsync("Invalid Game ID").ConfigureAwait(false);
                    return;
                }

                var url = await _service.GetBetPokemonGame(uid);
                if (url == null)
                {
                    await ctx.Channel.SendErrorAsync("Cannot find replay with that ID.").ConfigureAwait(false);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"{ctx.User.Mention} Here is the replay url:\nhttps://replay.pokemonshowdown.com/{url}"))
                    .ConfigureAwait(false);
            }
            
            private Image<Rgba32> GetPokemonImage(string pokemon, int generation)
            {
                var sprite = _service.GetPokemonSprite(pokemon);
                var wc = new WebClient();
                string genUrl;
                if (generation == 7)
                    genUrl = Gen5SpriteUrl;
                else if (generation == 6)
                    genUrl = Gen5SpriteUrl;
                else if (generation == 5)
                    genUrl = Gen5SpriteUrl;
                else if (generation == 4)
                    genUrl = Gen4SpriteUrl;
                else if (generation == 3)
                    genUrl = Gen3SpriteUrl;
                else if (generation == 2)
                    genUrl = Gen2SpriteUrl;
                else
                    genUrl = Gen1SpriteUrl;
                using var stream = new MemoryStream(wc.DownloadData(genUrl + sprite + ".png"));
                return Image.Load(stream.ToArray());
            }
        }
    }
}