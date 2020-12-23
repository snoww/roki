using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using HtmlAgilityPack;
using PlaywrightSharp;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Utility.Services
{
    public class NavigraphService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _http;

        private readonly WebClient _webClient;
        private IPlaywright _playwright;
        private IBrowser _browser;
        private IPage _page;
        private string _currentAirport;
        private SemaphoreSlim _semaphore;

        public NavigraphService(IRokiConfig config, IHttpClientFactory http)
        {
            _config = config;
            _webClient = new WebClient();
            _http = http;
        }

        private async Task CreateContext()
        {
            _semaphore = new SemaphoreSlim(1, 1);
            await _semaphore.WaitAsync();
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Firefox.LaunchAsync();
            _page = await _browser.NewPageAsync();
            await _page.GoToAsync("https://charts.navigraph.com/");
            await _page.ClickAsync("text=Sign in");
            await _page.WaitForTimeoutAsync(1500);
            await _page.FillAsync("id=username", _config.NavigraphUsername);
            await _page.FillAsync("id=password", _config.NavigraphPassword);
            await _page.PressAsync("id=username", "Enter");
            await _page.WaitForTimeoutAsync(1500);
            _semaphore.Release();
        }

        private async Task EnsureContextCreated()
        {
            if (_page == null)
            {
                await CreateContext();
                var _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(30));
                    await DisposeContext();
                });
            }
        }

        public async Task<string> GetSTAR(ICommandContext ctx, string icao, string star)
        {
            using var typing = ctx.Channel.EnterTypingState();
            await DownloadAllCharts(ctx, icao, "STAR");
            if (string.IsNullOrWhiteSpace(star))
            {
                await SendInfo(ctx, icao, "star");
                return null;
            }
            return GetChartName(icao, star);
        }
        
        public async Task<string> GetSID(ICommandContext ctx, string icao, string sid)
        {
            using var typing = ctx.Channel.EnterTypingState();
            await DownloadAllCharts(ctx, icao, "SID");
            if (string.IsNullOrWhiteSpace(sid))
            {
                await SendInfo(ctx, icao, "sid");
                return null;
            }
            return GetChartName(icao, sid);
        }

        public async Task<string> GetAPPR(ICommandContext ctx, string icao, string appr)
        {

            using var typing = ctx.Channel.EnterTypingState();
            await DownloadAllCharts(ctx, icao, "APPR");
            if (string.IsNullOrWhiteSpace(appr))
            {
                await SendInfo(ctx, icao, "appr");
                return null;
            }
            return GetChartName(icao, appr);
        }
        
        public async Task<string> GetTAXI(ICommandContext ctx, string icao, string taxi)
        {

            using var typing = ctx.Channel.EnterTypingState();
            await DownloadAllCharts(ctx, icao, "TAXI");
            if (string.IsNullOrWhiteSpace(taxi))
            {
                await SendInfo(ctx, icao, "taxi");
                return null;
            }
            return GetChartName(icao, taxi);
        }

        private static async Task SendInfo(ICommandContext ctx, string icao, string type)
        {
            var desc = await File.ReadAllLinesAsync($"data/charts/{icao}/{type}s.txt");
            if (desc.Sum(x => x.Length) >= 1990)
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()} 1/2")
                    .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', desc.Take(desc.Length / 2))}```")).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()} 2/2")
                    .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', desc.Skip(desc.Length / 2))}```")).ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()}")
                    .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', desc)}```")).ConfigureAwait(false);
            }
        }

        private static string GetChartName(string icao, string filename)
        {
            return filename == null 
                ? $"data/charts/{icao}/{icao}_NA.png".ToLowerInvariant() 
                : $"data/charts/{icao}/{icao}_{new string(Array.FindAll(filename.ToArray(), char.IsLetterOrDigit))}.png".ToLowerInvariant();
        }

        private async Task<bool> SetCurrentAirport(string icao)
        {
            if (_currentAirport != icao)
            {
                await _page.FillAsync("id=mat-input-0", icao);
                await _page.PressAsync("id=mat-input-0", "Enter");
                await _page.WaitForTimeoutAsync(1500);
                var selector = await _page.QuerySelectorAsync("css=.mat-focus-indicator.charts-btn.mat-mini-fab.mat-button-base.mat-primary.ng-star-inserted");
                if (selector == null)
                {
                    return false;
                }

                await selector.ClickAsync();
                _currentAirport = icao;
            }

            return true;
        }

        private async Task DownloadAllCharts(ICommandContext ctx, string icao, string type)
        {
            if (Directory.Exists($"data/charts/{icao}") && Directory.GetCreationTime($"data/charts/{icao}") - DateTime.UtcNow <= TimeSpan.FromDays(30))
            {
                return;
            }

            try
            {
                await EnsureContextCreated();
                await _semaphore.WaitAsync();
                if (!await SetCurrentAirport(icao))
                {
                    await ctx.Channel.SendErrorAsync("Invalid ICAO");
                    return;
                }

                var chartIds = new Dictionary<string, string>();
                var htmlDoc = new HtmlDocument();

                // get STARs
                await _page.WaitForTimeoutAsync(1000);
                await _page.ClickAsync("//html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/chart-filter/div/button[1]");
                await _page.WaitForTimeoutAsync(1500);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                var starNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                Directory.CreateDirectory($"data/charts/{icao}");
                var stars = new StringBuilder();
                foreach (var htmlNode in starNodes)
                {
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    var idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                    chartIds.TryAdd(nameNode, idNode);
                    stars.AppendLine(nameNode);
                }
                await File.WriteAllTextAsync($"data/charts/{icao}/stars.txt", stars.ToString());

                // get SIDSs
                await _page.ClickAsync("//html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/chart-filter/div/button[4]");
                await _page.WaitForTimeoutAsync(1500);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                var sidNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                var sids = new StringBuilder();
                foreach (var htmlNode in sidNodes)
                {
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    var idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                    chartIds.TryAdd(nameNode, idNode);
                    sids.AppendLine(nameNode);
                }
                await File.WriteAllTextAsync($"data/charts/{icao}/sids.txt", sids.ToString());

                // get APPRs
                await _page.ClickAsync("//html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/chart-filter/div/button[2]");
                await _page.WaitForTimeoutAsync(1500);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                var apprNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                var apprs = new StringBuilder();
                foreach (var htmlNode in apprNodes)
                {
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    var idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                    chartIds.TryAdd(nameNode, idNode);
                    apprs.AppendLine(nameNode);
                }
                await File.WriteAllTextAsync($"data/charts/{icao}/apprs.txt", apprs.ToString());

                // get TAXIs
                await _page.ClickAsync("//html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/chart-filter/div/button[3]");
                await _page.WaitForTimeoutAsync(1500);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                var taxiNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                var taxis = new StringBuilder();
                foreach (var htmlNode in taxiNodes)
                {
                    var idNode = htmlNode.SelectSingleNode("div/div[3]/small");
                    if (idNode == null)
                        continue;
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    chartIds.TryAdd(nameNode, HtmlEntity.DeEntitize(idNode.InnerText));
                    taxis.AppendLine(nameNode);
                }
                await File.WriteAllTextAsync($"data/charts/{icao}/taxis.txt", taxis.ToString());
                
                var element = await _page.EvaluateAsync("window.localStorage.getItem(\"access_token\")");
                var token = element?.GetString();
                using var http = _http.CreateClient();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                Directory.CreateDirectory($"data/charts/{icao}");
                foreach (var (name, id) in chartIds)
                {
                    var imageUrl = await http.GetStringAsync(
                        $"https://charts.api.navigraph.com/2/airports/{icao.ToUpperInvariant()}/signedurls/{icao.ToLowerInvariant()}{new string(Array.FindAll(id.ToArray(), char.IsLetterOrDigit)).ToLowerInvariant()}.png");
                    GetImage(imageUrl, GetChartName(icao, name));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await ctx.Channel.SendErrorAsync("Something went wrong, please try again.");
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        private void GetImage(string url, string filename)
        {
            _webClient.DownloadFile(url, filename);
        }

        private async Task DisposeContext()
        {
            await _page.CloseAsync();
            await _browser.CloseAsync();
            _playwright.Dispose();
            _semaphore.Dispose();
            _page = null;
            _browser = null;
            _playwright = null;
            _semaphore = null;
        }
    }
}