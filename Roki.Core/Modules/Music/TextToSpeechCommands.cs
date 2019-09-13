using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;
using Roki.Common.Attributes;
using Roki.Extensions;

namespace Roki.Modules.Music
{
    public partial class Music
    {
        [Group]
        public class TextToSpeechCommands : RokiSubmodule
        {
            private readonly string _api = "https://streamlabs.com/polly/speak";
            private string _queryJson = @"{""voice"": ""Brian"", ""text"": ""textString""}";
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Tts([Leftover] string query = null)
            {
//                query = query.SanitizeString();
//                unsure if sanitize removes spaces

                if (string.IsNullOrWhiteSpace(query))
                {
                    await ctx.Channel.SendErrorAsync("No text provided.").ConfigureAwait(false);
                    return;
                }

                using (var http = new HttpClient())
                {
                    var queryString = _queryJson.Replace("textString", query);
                    var content = new StringContent(queryString, Encoding.UTF8, "application/json");
                    var result = await http.PostAsync(_api, content).ConfigureAwait(false);

                    var tts = JsonConvert.DeserializeObject<TtsModel>(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                    if (tts.Success)
                    {
                        using (var client = new WebClient())
                        {
                            await client.DownloadFileTaskAsync(tts.SpeakUrl, "./temp/tts.ogg").ConfigureAwait(false);
                        }

                        await ctx.Channel.SendFileAsync("./temp/tts.ogg").ConfigureAwait(false);
                        File.Delete("./temp/tts.ogg");
                    }
                    else
                    {
                        await ctx.Channel.SendErrorAsync("Something went wrong with the TTS.").ConfigureAwait(false);
                    }
                }
                
            }
        }
    }

    public class TtsModel
    {
        public bool Success { get; set; }
        [JsonProperty("speak_url")]
        public string SpeakUrl { get; set; }
    }
}