using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Customsearch.v1;
using Google.Apis.Customsearch.v1.Data;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Roki.Extensions;

namespace Roki.Services
{
    public interface IGoogleApiService : IRokiService
    {
        Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1);
        Task<IEnumerable<(string Name, string Id, string Url)>> GetVideoInfoByKeywordAsync(string keywords, int count = 1);
        Task<ImageResult> GetImagesAsync(string query, bool random = false);
    }

    public class ImageResult
    {
        public Result.ImageData Image { get; set; }
        public string Link { get; set; }
    }
    
    public class GoogleApiService : IGoogleApiService
    {
        private const string SearchEngineId = "009851753967553605166:ffip6ctnuaq";

        private readonly CustomsearchService _cs;

        private readonly YouTubeService _yt;

        public GoogleApiService(IRokiConfig config)
        {
            var baseClient = new BaseClientService.Initializer
            {
                ApplicationName = "Roki",
                ApiKey = config.GoogleApi
            };
            
            _yt = new YouTubeService(baseClient);
            _cs = new CustomsearchService(baseClient);
        }
        
        public async Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var query = _yt.Search.List("snippet");
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
            var query = _yt.Search.List("snippet");
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

            var request = _cs.Cse.List(query);
            var start = random ? new Random().Next(1, 10) : 1;
            request.Cx = SearchEngineId;
            request.Fields = "items(image(contextLink,thumbnailLink),link)";
            request.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            request.Start = start;
            var search = await request.ExecuteAsync().ConfigureAwait(false);
            return random
                ? new ImageResult{Image = search.Items[start].Image, Link = search.Items[start].Link}
                : new ImageResult{Image = search.Items[0].Image, Link = search.Items[0].Link};
        }
    }
}