using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;

namespace Roki.Modules.Music
{
    public partial class Music
    {
        [Group]
        public class TextToSpeechCommands : RokiSubmodule
        {
            private const string StreamlabsApi = "https://streamlabs.com/polly/speak";
            private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};
            
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

                if (query.Length > 300)
                {
                    await ctx.Channel.SendErrorAsync("TTS message too long. Max 300 characters.");
                    return;
                }

                using var http = new HttpClient();
                var queryRequest = JsonSerializer.Serialize(new TtsRequest {Text = query});
                var content = new StringContent(queryRequest, Encoding.UTF8, "application/json");
                var result = await http.PostAsync(StreamlabsApi, content).ConfigureAwait(false);

                if (!result.IsSuccessStatusCode)
                {
                    await ctx.Channel.SendErrorAsync("Streamlabs TTS error. Please try again later.").ConfigureAwait(false);
                    return;
                }

                var tts = JsonSerializer.Deserialize<TtsModel>(await result.Content.ReadAsStringAsync().ConfigureAwait(false), Options);
                if (tts.Success)
                {
                    var guid = Guid.NewGuid().ToString().Substring(0, 7);
                    var fileName = $"{ctx.User.Username}-{guid}";
                    using (var client = new WebClient())
                    {
                        await client.DownloadFileTaskAsync(tts.SpeakUrl, $"./temp/{fileName}.ogg").ConfigureAwait(false);
                    }

                    using (var proc = new Process())
                    {
                        proc.StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-i ./temp/tts-{guid}.ogg ./temp/tts-{guid}.mp3",
                            RedirectStandardOutput = false,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        };
                        proc.Start();
                        proc.WaitForExit();
                    }

                    await ctx.Channel.SendFileAsync($"./temp/{fileName}.mp3").ConfigureAwait(false);
                    File.Delete($"./temp/{fileName}.ogg");
                    File.Delete($"./temp/{fileName}.mp3");
                }
                else
                {
                    await ctx.Channel.SendErrorAsync("Streamlabs TTS error. Please try again later.").ConfigureAwait(false);
                }
            }
        }
    }

    public class TtsRequest
    {
        [JsonPropertyName("voice")]
        public string Voice { get; set; } = "Brian";
        
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
    
    public class TtsModel
    {
        public bool Success { get; set; }
        
        [JsonPropertyName("speak_url")]
        public string SpeakUrl { get; set; }
    }
}