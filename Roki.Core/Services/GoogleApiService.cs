using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Apis.Customsearch.v1;
using Google.Apis.Customsearch.v1.Data;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using NLog;
using Roki.Extensions;

namespace Roki.Core.Services
{
    public interface IGoogleApiService : IRService
    {
        Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1);
        Task<IEnumerable<(string Name, string Id, string Url)>> GetVideoInfoByKeywordAsync(string keywords, int count = 1);
        Task<ImageResult> GetImagesAsync(string query, bool random = false);
        Task<string> GetRelatedVideo(string videoId);
        Task<string> GetRelatedVideoByQuery(string keywords);
    }

    public struct ImageResult
    {
        public Result.ImageData Image { get; }
        public string Link { get; }

        public ImageResult(Result.ImageData image, string link)
        {
            Image = image;
            Link = link;
        }
    }
    
    public class GoogleApiService : IGoogleApiService
    {
        private const string search_engine_id = "009851753967553605166:ffip6ctnuaq";

        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly CustomsearchService cs;

        private readonly YouTubeService yt;

        public GoogleApiService(IRokiConfig config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _httpFactory = httpFactory;

            var baseClient = new BaseClientService.Initializer
            {
                ApplicationName = "Roki",
                ApiKey = _config.GoogleApi
            };

            _log = LogManager.GetCurrentClassLogger();

            yt = new YouTubeService(baseClient);
            cs = new CustomsearchService(baseClient);
        }

        private Logger _log { get; }

        public async Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var query = yt.Search.List("snippet");
            query.MaxResults = count;
            query.Q = keywords;
            query.Type = "video";
            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i => "http://www.youtube.com/watch?v=" + i.Id.VideoId);
        }

        public async Task<IEnumerable<(string Name, string Id, string Url)>> GetVideoInfoByKeywordAsync(string keywords, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            var query = yt.Search.List("snippet");
            query.MaxResults = count;
            query.Q = keywords;
            query.Type = "video";
            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i =>
                (i.Snippet.Title.TrimTo(50), i.Id.VideoId, "http://www.youtube.com/watch?v=" + i.Id.VideoId));
        }

        public async Task<ImageResult> GetImagesAsync(string query, bool random = false)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            var request = cs.Cse.List(query);
            var start = random ? new Random().Next(1, 10) : 1;
            request.Cx = search_engine_id;
            request.Fields = "items(image(contextLink,thumbnailLink),link)";
            request.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            request.Start = start;
            var search = await request.ExecuteAsync().ConfigureAwait(false);
            return random
                ? new ImageResult(search.Items[start].Image, search.Items[start].Link)
                : new ImageResult(search.Items[0].Image, search.Items[0].Link);
        }

        public async Task<string> GetRelatedVideo(string videoId)
        {
            var query = yt.Search.List("snippet");
            query.MaxResults = 1;
            query.RelatedToVideoId = videoId;
            query.Type = "video";
            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.FirstOrDefault()?.Id.VideoId;
        }

        public async Task<string> GetRelatedVideoByQuery(string keywords)
        {
            var query = yt.Search.List("snippet");
            query.MaxResults = 1;
            query.Q = keywords;
            query.Type = "video";
            return await GetRelatedVideo((await query.ExecuteAsync().ConfigureAwait(false)).Items.FirstOrDefault()?.Id.VideoId).ConfigureAwait(false);
        }
    }
}