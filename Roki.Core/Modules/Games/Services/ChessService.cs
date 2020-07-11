using System;
using System.Collections.Generic;
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

        public async Task<string> CreateChessChallenge(ChessArgs args)
        {
            using var http = _http.CreateClient();
            var opts = new Dictionary<string, string>
            {
                {"clock.limit", (args.Time * 60).ToString()},
                {"clock.increment", args.Increment.ToString()}
            };
            
            var response = await http.PostAsync(LichessApi + "challenge/open", new FormUrlEncodedContent(opts)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var json = JsonDocument.Parse(raw);
            return json.RootElement.GetProperty("challenge").GetProperty("url").GetString();
        }

        public void PollGame(ICommandContext ctx, ChessArgs opts, string challengeUrl)
        {
            var _ = Task.Run(async () =>
            {
                var gameId = challengeUrl.Substring(challengeUrl.LastIndexOf('/'));
                using var http = _http.CreateClient();
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                var response = await http.GetStringAsync($"https://lichess.org/game/export{gameId}").ConfigureAwait(false);
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
                    
                Enum.TryParse(json.RootElement.GetProperty("status").GetString(), out ChessResult result);

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