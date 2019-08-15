using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Apis.Customsearch.v1;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using NLog;
using Roki.Core.Extentions;

namespace Roki.Core.Services.Impl
{
    public class GoogleApiService : IGoogleApiService
    {
        private const string search_engine_id = "009851753967553605166:ffip6ctnuaq";
        
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;

        private YouTubeService yt;
        private CustomsearchService cs;
        
        private Logger _log { get; }

        public GoogleApiService(IConfiguration config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _httpFactory = httpFactory;
            
            var bcs = new BaseClientService.Initializer 
            {
                ApplicationName = "Roki",
                ApiKey = _config.GoogleApi,
            };

            _log = LogManager.GetCurrentClassLogger();
            
            yt = new YouTubeService();
            cs = new CustomsearchService();
        }

        public async Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var query = yt.Search.List("snippet");
            query.Key = _config.GoogleApi;
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
            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i => (i.Snippet.Title.TrimTo(50), i.Id.VideoId, "http://www.youtube.com/watch?v=" + i.Id.VideoId));
        }

        public async Task<ImageResult> GetImagesAsync(string query, bool random = false)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            var request = cs.Cse.List(query);
            request.Cx = search_engine_id;
            request.Num = 1;
            request.Fields = "items(image(contextLink,thumbnailLink),link";
            request.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            request.Start = random ? new Random().Next(1, 15) : 1;
            var search = await request.ExecuteAsync().ConfigureAwait(false);
            return new ImageResult(search.Items[0].Image, search.Items[0].Link);
        }
    }
}