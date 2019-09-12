using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Searches.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class AnimeCommands : RokiSubmodule<AnimeService>
        {
            public async Task Anime([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await ctx.Channel.SendErrorAsync("No query provided.").ConfigureAwait(false);
                    return;
                }

                var media = await _service.GetAnimeDataAsync(query).ConfigureAwait(false);
                if (media == null)
                {
                    await ctx.Channel.SendErrorAsync("Couldn't find that anime :(").ConfigureAwait(false);
                    return;
                }

                EmbedBuilder AnimeList(int curPage)
                {
                    var a = new EmbedBuilder().WithImageUrl("asd");
                    var item = media[curPage];
                    var embed = new EmbedBuilder().WithColor(Convert.ToUInt32(item.coverImage.color.ToString().Substring(1), 16))
                        .WithTitle((item.title.english + " | " + item.title.romaji + " | " + item.title.native).ToString().TrimTo(256))
                        .WithDescription(item.description.ToString().TrimTo(2048))
                        .WithImageUrl(item.coverImage.large)
                        .AddField("Type", item.type, true)
                        .AddField("Genres", string.Join(", ", item.genres), true)
                        .AddField("Status", item.status, true)
                        .AddField("Episodes", item.episodes, true)
                        .AddField("Release Year", item.seasonInt.GetReleaseYear(), true)
                        .AddField("Rating", $"{item.averageScore}/100");
                    return embed;
                }

                await ctx.SendPaginatedConfirmAsync((int) 0, (Func<int, EmbedBuilder>) AnimeList, (int) media.Count, (int) 1, (bool) false).ConfigureAwait(false);
            }
        }
    }
    public static class AnimeExtensions
    {
        public static string GetReleaseYear(int seasonInt)
        {
            var year = seasonInt.ToString().Substring(0, seasonInt.ToString().Length - 1);
            var season = seasonInt.ToString().Substring(seasonInt.ToString().Length - 1);
            
            if (string.IsNullOrWhiteSpace(year))
                year = "2000";
            else if (int.Parse(year) > 0 && int.Parse(year) < 10)
                year = "200" + year;
            else if (int.Parse(year) > 30)
                year = "19" + year;
            
            switch (season)
            {
                case "1":
                    season = "Spring";
                    break;
                case "2":
                    season = "Summer";
                    break;
                case "3":
                    season = "Fall";
                    break;
                case "4":
                    season = "Winter";
                    break;
            }

            return season + " " + year;
        }
    }
}