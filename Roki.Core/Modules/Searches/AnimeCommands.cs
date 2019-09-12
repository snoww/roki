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
            [RokiCommand, Usage, Description, Aliases]
            public async Task Anime([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await ctx.Channel.SendErrorAsync("No query provided.").ConfigureAwait(false);
                    return;
                }

                var media = await _service.GetAnimeDataAsync(query).ConfigureAwait(false);
                if ((int) media.Count < 1)
                {
                    await ctx.Channel.SendErrorAsync("Couldn't find that anime :(").ConfigureAwait(false);
                    return;
                }

                EmbedBuilder AnimeList(int curPage)
                {
                    var item = media[curPage];
                    var title = ((string) (item.title.english + " | " + item.title.romaji + " | " + item.title.native).ToString()).TrimTo(256);
                    var desc = ((string) item.description).TrimTo(2048).StripHtml();
                    var release = ((int) item.seasonInt).GetReleaseYear();

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(title)
                        .WithThumbnailUrl(item.coverImage.large.ToString())
                        .WithDescription(desc)
                        .AddField("Type", ((string) item.type).ToTitleCase(), true)
                        .AddField("Genres", string.Join(", ", item.genres), true)
                        .AddField("Status", ((string) item.status).ToTitleCase(), true)
                        .AddField("Episodes", item.episodes, true)
                        .AddField("Release Year", release, true)
                        .AddField("Rating", $"{item.averageScore} / 100", true);
                   
                    return embed;
                }

                await ctx.SendPaginatedConfirmAsync( 0, AnimeList, (int) media.Count, 1, false).ConfigureAwait(false);
            }
        }
    }
    public static class AnimeExtensions
    {
        public static string GetReleaseYear(this int seasonInt)
        {
            var year = seasonInt.ToString().Substring(0, seasonInt.ToString().Length - 1);
            var season = seasonInt.ToString().Substring(seasonInt.ToString().Length - 1);
            
            if (string.IsNullOrWhiteSpace(year))
                year = "2000";
            else if (int.Parse(year) > 0 && int.Parse(year) < 10)
                year = "200" + year;
            else if (int.Parse(year) > 30)
                year = "19" + year;
            else
                year = "20" + year;

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
                default:
                    season = "Spring";
                    break;
            }

            return season + " " + year;
        }
    }
}