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

                if (query.Length > 300)
                {
                    await ctx.Channel.SendErrorAsync("TTS message too long. Max 300 characters.");
                    return;
                }

                using (var http = new HttpClient())
                {
                    var queryString = _queryJson.Replace("textString", query);
                    var content = new StringContent(queryString, Encoding.UTF8, "application/json");
                    var result = await http.PostAsync(_api, content).ConfigureAwait(false);

                    var tts = JsonSerializer.Deserialize<TtsModel>(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                    if (tts.Success)
                    {
                        var guid = Guid.NewGuid().ToString().Substring(0, 7);
                        using (var client = new WebClient())
                        {
                            await client.DownloadFileTaskAsync(tts.SpeakUrl, $"./temp/tts-{guid}.ogg").ConfigureAwait(false);
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

                        await ctx.Channel.SendFileAsync($"./temp/tts-{guid}.mp3").ConfigureAwait(false);
                        File.Delete($"./temp/tts-{guid}.ogg");
                        File.Delete($"./temp/tts-{guid}.mp3");
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
        [JsonPropertyName("speak_url")]
        public string SpeakUrl { get; set; }
    }
}