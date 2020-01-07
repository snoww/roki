using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using Roki.Core.Services;

namespace Roki.Modules.Utility.Services
{
    public class QuoteService : IRokiService
    {
        public bool IsImage(string text)
        {
            if (!Uri.IsWellFormedUriString(text, UriKind.Absolute)) return false;
            var request = (HttpWebRequest) WebRequest.Create(text);
            request.Method = "HEAD";
            using var resp = request.GetResponse();
            return resp.ContentType.ToLower(CultureInfo.InvariantCulture).StartsWith("image/");
        }
    }
}