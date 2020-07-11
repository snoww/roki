using System;
using System.Text.Json;
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

                var response = await Service.CreateChessChallenge(opts).ConfigureAwait(false);
                using var json = JsonDocument.Parse(response);
                var challenge = json.RootElement.GetProperty("challenge");
                var challengeUrl = challenge.GetProperty("url").GetString();
                var speed = $"{challenge.GetProperty("timeControl").GetProperty("show").GetString()} {challenge.GetProperty("speed").GetString().ToTitleCase()}";
 
                if (string.IsNullOrWhiteSpace(challengeUrl))
                {
                    await Context.Channel.SendErrorAsync("Something went wrong when creating the challenge.\nPlease try again later");
                    return;
                }

                var userDm = await Context.User.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                var oppDm = await opponent.GetOrCreateDMChannelAsync().ConfigureAwait(false);

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Chess Challenge")
                        .WithAuthor(speed)
                        .WithDescription($"{Context.User.Mention} vs {opponent.Mention}\nCheck DMs for challenge link."))
                    .ConfigureAwait(false);

                try
                {
                    if (opts.Color == ChessColor.Random)
                    {
                        await userDm.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle($"Chess Challenge vs {opponent}")
                                .WithAuthor(speed)
                                .WithDescription($"[Click here for Challenge Link]({challengeUrl})"))
                            .ConfigureAwait(false);
                    
                        await oppDm.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle($"Chess Challenge vs {Context.User}")
                                .WithAuthor(speed)
                                .WithDescription($"[Click here for Challenge Link]({challengeUrl})"))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await userDm.EmbedAsync(new EmbedBuilder().WithColor(opts.Color == ChessColor.White ? new Color(255, 255, 255) : new Color(0, 0, 0))
                                .WithTitle($"Chess Challenge vs {opponent}")
                                .WithAuthor($"{speed} as {opts.Color}")
                                .WithDescription($"[Click here for Challenge Link]({challengeUrl}{(opts.Color == ChessColor.White ? "?color=white" : "?color=black")})"))
                            .ConfigureAwait(false);
                    
                        await oppDm.EmbedAsync(new EmbedBuilder().WithColor(opts.Color == ChessColor.Black ? new Color(255, 255, 255) : new Color(0, 0, 0))
                                .WithTitle($"Chess Challenge vs {Context.User}")
                                .WithAuthor($"{speed} as {opts.Color}")
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
                                .WithAuthor(speed)
                                .WithDescription($"{Context.User.Mention} vs {opponent.Mention}\n[Click here for Challenge Link]({challengeUrl})"))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle("Chess Challenge")
                                .WithAuthor($"{speed} as {opts.Color}")
                                .WithDescription($"{Context.User.Mention} vs {opponent.Mention}\n[Click here for Challenge Link]({challengeUrl}{(opts.Color == ChessColor.White ? "?color=white" : "?color=black")})"))
                            .ConfigureAwait(false);
                    }
                }

                Service.PollGame(Context, opts, challengeUrl, speed);
            }
        }
    }
}