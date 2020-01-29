using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Modules.Searches.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class AnimeCommands : RokiSubmodule<AnimeService>
        {
            [RokiCommand, Usage, Description, Aliases]
            public async Task Anime([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await ctx.Channel.SendErrorAsync("No query provided.").ConfigureAwait(false);
                    return;
                }

                var media = await _service.GetAnimeDataAsync(query).ConfigureAwait(false);
                if (media.Count < 1)
                {
                    await ctx.Channel.SendErrorAsync("Couldn't find that anime :(").ConfigureAwait(false);
                    return;
                }

                await ctx.SendPaginatedConfirmAsync( 0, p =>
                {
                    var anime = media[p];
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(anime.Title.GetTitle())
                        .WithImageUrl(anime.CoverImage.Large)
                        .WithDescription(HttpUtility.HtmlDecode(anime.Description).TrimTo(2048))
                        .AddField("Type", anime.Type.ToTitleCase(), true)
                        .AddField("Status", anime.Status.ToTitleCase(), true)
                        .AddField("Episodes", anime.Episodes != null ? anime.Episodes.ToString() : "N/A", true)
                        .AddField("Release Year", anime.SeasonInt.GetReleaseYear(), true)
                        .AddField("Rating", anime.AverageScore != null ? $"{anime.AverageScore}  / 100" : "N/A", true)
                        .AddField("Genres", string.Join(", ", anime.Genres), true);
                    return embed;
                }, media.Count, 1).ConfigureAwait(false);
            }
        }
    }
    
    public static class AnimeExtensions
    {
        public static string GetTitle(this AnimeTitle title)
        {
            var titles = new List<string>();

            if (!string.IsNullOrWhiteSpace(title.Romaji))
                titles.Add(title.Romaji);
            if (!string.IsNullOrWhiteSpace(title.English))
                titles.Add(title.English);
            if (!string.IsNullOrWhiteSpace(title.Native))
                titles.Add(title.Native);

            return string.Join(" | ", titles);
        }
        
        public static string GetReleaseYear(this int? seasonInt)
        {
            if (seasonInt == null)
                return "N/A";
            
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