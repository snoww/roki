using Google.Apis.Customsearch.v1.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Roki.Core.Services
{
    public interface IGoogleApiService :INService
    {
        Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1);
        Task<IEnumerable<(string Name, string Id, string Url)>> GetVideoInfoByKeywordAsync(string keywords, int count = 1);
        Task<ImageResult> GetImagesAsync(string query);
    }

    public struct ImageResult
    {
        public Result.ImageData Image { get; }
        public string Link { get; }

        public ImageResult(Result.ImageData image, string link)
        {
            this.Image = image;
            this.Link = link;
        }
    }
}