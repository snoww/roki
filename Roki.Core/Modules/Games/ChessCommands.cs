using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Modules.Games.Services;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class ChessCommands : RokiSubmodule<ChessService>
        {
            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            [RokiOptions(typeof(ChessArgs))]
            public async Task ChessChallenge(IGuildUser opponent, params string[] args)
            {
                if (opponent == Context.User)
                {
                    await Context.Channel.SendErrorAsync("You cannot challenge yourself!").ConfigureAwait(false);
                    return;
                }

                if (opponent.IsBot)
                {
                    await Context.Channel.SendErrorAsync("You cannot challenge a bot (yet)!").ConfigureAwait(false);
                    return;
                }
                var opts = OptionsParser.ParseFrom(new ChessArgs(), args);
                opts.ChallengeTo = opponent;

                var challengeUrl = await Service.CreateChessChallenge(opts).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(challengeUrl))
                {
                    await Context.Channel.SendErrorAsync("Something went wrong when creating the challenge.\nPlease try again later");
                    return;
                }

                var userDm = await Context.User.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                var oppDm = await opponent.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                
                try
                {
                    if (opts.Color == ChessColor.Random)
                    {
                        await userDm.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle($"Chess Challenge vs {opponent}")
                                .WithAuthor($"{opts.Time}+{opts.Increment}")
                                .WithDescription($"[Click here for Challenge Link]({challengeUrl})"))
                            .ConfigureAwait(false);
                    
                        await oppDm.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle($"Chess Challenge vs {Context.User}")
                                .WithAuthor($"{opts.Time}+{opts.Increment}")
                                .WithDescription($"[Click here for Challenge Link]({challengeUrl})"))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await userDm.EmbedAsync(new EmbedBuilder().WithColor(opts.Color == ChessColor.White ? new Color(0xFFFFFF) : new Color(0x0))
                                .WithTitle($"Chess Challenge vs {opponent}")
                                .WithAuthor($"{opts.Time}+{opts.Increment} as {opts.Color}")
                                .WithDescription($"[Click here for Challenge Link]({challengeUrl}{(opts.Color == ChessColor.White ? "?color=white" : "?color=black")})"))
                            .ConfigureAwait(false);
                    
                        await oppDm.EmbedAsync(new EmbedBuilder().WithColor(opts.Color == ChessColor.Black ? new Color(0xFFFFFF) : new Color(0x0))
                                .WithTitle($"Chess Challenge vs {Context.User}")
                                .WithAuthor($"{opts.Time}+{opts.Increment} as {opts.Color}")
                                .WithDescription($"[Click here for Challenge Link]({challengeUrl}{(opts.Color == ChessColor.Black ? "?color=white" : "?color=black")})"))
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    if (opts.Color == ChessColor.Random)
                    {
                        await userDm.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle("Chess Challenge")
                                .WithAuthor($"{opts.Time}+{opts.Increment}")
                                .WithDescription($"{Context.User.Mention} vs {opponent.Mention}\n[Click here for Challenge Link]({challengeUrl})"))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle("Chess Challenge")
                                .WithAuthor($"{opts.Time}+{opts.Increment} as {opts.Color}")
                                .WithDescription($"{Context.User.Mention} vs {opponent.Mention}\n[Click here for Challenge Link]({challengeUrl}{(opts.Color == ChessColor.White ? "?color=white" : "?color=black")})"))
                            .ConfigureAwait(false);
                    }
                }

                Service.PollGame(Context, opts, challengeUrl);
            }
        }
    }
}