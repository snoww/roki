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
            private static readonly string TempDir = Path.GetTempPath();

            private readonly IHttpClientFactory _factory;

            public TextToSpeechCommands(IHttpClientFactory factory)
            {
                _factory = factory;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Tts([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await Context.Channel.SendErrorAsync("No text provided.").ConfigureAwait(false);
                    return;
                }

                if (query.Length > 300)
                {
                    await Context.Channel.SendErrorAsync("TTS message too long. Max 300 characters.");
                    return;
                }

                using HttpClient http = _factory.CreateClient();
                var content = new StringContent(JsonSerializer.Serialize(new TtsRequest {Text = query}), Encoding.UTF8, "application/json");
                HttpResponseMessage result = await http.PostAsync(StreamlabsApi, content).ConfigureAwait(false);

                if (!result.IsSuccessStatusCode)
                {
                    await Context.Channel.SendErrorAsync("Streamlabs TTS error. Please try again later.").ConfigureAwait(false);
                    return;
                }

                var tts = (await result.Content.ReadAsStringAsync().ConfigureAwait(false)).Deserialize<TtsModel>();
                if (tts.Success)
                {
                    string guid = Guid.NewGuid().ToSubGuid();
                    var filePath = $"{TempDir}{Context.User.Username}-{guid}";
                    using (var client = new WebClient())
                    {
                        await client.DownloadFileTaskAsync(tts.SpeakUrl, filePath + ".ogg").ConfigureAwait(false);
                    }

                    using (var proc = new Process())
                    {
                        proc.StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-i {filePath}.ogg {filePath}.mp3",
                            RedirectStandardOutput = false,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        proc.Start();
                        await proc.WaitForExitAsync();
                    }

                    await Context.Channel.SendFileAsync($"{filePath}.mp3").ConfigureAwait(false);
                    File.Delete($"{filePath}.ogg");
                    File.Delete($"{filePath}.mp3");
                }
                else
                {
                    await Context.Channel.SendErrorAsync("Streamlabs TTS error. Please try again later.").ConfigureAwait(false);
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