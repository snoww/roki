using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Discord;
using Discord.Commands;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Services;

namespace Roki.Modules.Games.Services
{
    public class ChessService : IRokiService
    {
        private readonly IHttpClientFactory _http;
        private const string LichessApi = "https://lichess.org/api/";
        
        public ChessService(IHttpClientFactory http)
        {
            _http = http;
        }

        public async Task<string> CreateChessChallenge(ChessOptions options)
        {
            using var http = _http.CreateClient();
            var opts = new Dictionary<string, string>
            {
                {"clock.limit", (options.Time * 60).ToString()},
                {"clock.increment", options.Increment.ToString()}
            };
            
            var response = await http.PostAsync(LichessApi + "challenge/open", new FormUrlEncodedContent(opts)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public void PollGame(ICommandContext ctx, ChessOptions opts, string challengeUrl, string speed)
        {
            var _ = Task.Run(async () =>
            {
                var gameId = challengeUrl.Substring(challengeUrl.LastIndexOf('/'));
                using var http = _http.CreateClient();
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                var retry = 0;
                string response;
                do
                {
                    try
                    {
                        response = await http.GetStringAsync($"https://lichess.org/game/export{gameId}").ConfigureAwait(false);
                        goto found;
                    }
                    catch (HttpRequestException)
                    {
                        // retry
                    }
                    
                    await Task.Delay(20000);
                    retry++;
                } while (retry < 15);
                
                return;
                
                found:
                
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithTitle($"Chess Challenge {ctx.User} vs {opts.ChallengeTo}")
                        .WithAuthor(speed)
                        .WithDescription($"[Click here for to spectate game]({challengeUrl})"))
                    .ConfigureAwait(false);
                
                using var json = JsonDocument.Parse(response);

                var counter = 0;
                var maxCounter = opts.Time * 60 * 3;
                while (!json.RootElement.TryGetProperty("winner", out var _))
                {
                    await Task.Delay(2000);
                    counter += 2;
                    if (counter >= maxCounter)
                    {
                        return;
                    }
                }

                json.RootElement.TryGetProperty("winner", out var winner);
                
                var description = new StringBuilder();
                description.AppendLine($"{ctx.User.Mention} vs {opts.ChallengeTo.Mention}");
                    
                Enum.TryParse(json.RootElement.GetProperty("status").GetString().ToTitleCase(), out ChessResult result);

                if (result == ChessResult.Draw)
                {
                    description.AppendLine("Game resulted in a Draw");
                }
                else if (result == ChessResult.Mate)
                {
                    description.AppendLine(winner.GetString().Equals("white", StringComparison.Ordinal) ? "White won with Checkmate" : "Black won with Checkmate");
                }
                else if (result == ChessResult.Resign)
                {
                    description.AppendLine(winner.GetString().Equals("white", StringComparison.Ordinal) ? "White won with Black's Resignation" : "Black won with White's Resignation");
                }
                else if (result == ChessResult.Outoftime)
                {
                    description.AppendLine(winner.GetString().Equals("white", StringComparison.Ordinal) ? "White won by Timeout" : "Black won by Timeout");
                }
                else
                {
                    description.AppendLine("Game was Aborted");
                }
                    
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithTitle("Chess Challenge")
                        .WithAuthor($"{opts.Time}+{opts.Increment}")
                        .WithDescription(description.ToString()))
                    .ConfigureAwait(false);
            });
        }
        
        private enum ChessResult
        {
            Draw,
            Mate,
            Resign,
            Outoftime,
            Aborted,
        }
    }
}