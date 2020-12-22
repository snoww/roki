using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
            return GetChartName(icao, star);
        }
        
        public async Task<string> GetSID(ICommandContext ctx, string icao, string sid)
        {
            using var typing = ctx.Channel.EnterTypingState();
            await DownloadAllCharts(ctx, icao, "SID");
            return GetChartName(icao, sid);
        }

        public async Task<string> GetAPPR(ICommandContext ctx, string icao, string appr)
        {

            using var typing = ctx.Channel.EnterTypingState();
            await DownloadAllCharts(ctx, icao, "APPR");
            return GetChartName(icao, appr);
        }
        
        public async Task<string> GetTAXI(ICommandContext ctx, string icao, string taxi)
        {

            using var typing = ctx.Channel.EnterTypingState();
            await DownloadAllCharts(ctx, icao, "TAXI");
            return GetChartName(icao, taxi);
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
                var selectedNames = new List<string>();
                
                // get STARs
                await _page.WaitForTimeoutAsync(1000);
                await _page.ClickAsync("css=.mat-focus-indicator.clr-star >> text=STAR");
                await _page.WaitForTimeoutAsync(1000);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                var nodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                foreach (var htmlNode in nodes)
                {
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    var idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                    if (type == "STAR") selectedNames.Add(nameNode);
                    chartIds.TryAdd(nameNode, idNode);
                }
                
                // get SIDSs
                await _page.ClickAsync("css=.mat-focus-indicator.clr-sid >> text=SID");
                await _page.WaitForTimeoutAsync(1000);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                nodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                foreach (var htmlNode in nodes)
                {
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    var idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                    if (type == "SID") selectedNames.Add(nameNode);
                    chartIds.TryAdd(nameNode, idNode);
                }
                
                // get APPRs
                await _page.ClickAsync("css=.mat-focus-indicator.clr-app >> text=APP");
                await _page.WaitForTimeoutAsync(1000);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                nodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                foreach (var htmlNode in nodes)
                {
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    var idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                    if (type == "APPR") selectedNames.Add(nameNode);
                    chartIds.TryAdd(nameNode, idNode);
                }
                
                // get TAXIs
                await _page.ClickAsync("css=.mat-focus-indicator.clr-taxi >> text=TAXI");
                await _page.WaitForTimeoutAsync(1000);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                nodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                foreach (var htmlNode in nodes)
                {
                    var nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                    var idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                    if (type == "TAXI") selectedNames.Add(nameNode);
                    chartIds.TryAdd(nameNode, idNode);
                }

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

                var desc = string.Join('\n', selectedNames);
                if (desc.Length >= 1990)
                {
                    await ctx.Channel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor().WithTitle($"{type} results for {icao.ToUpperInvariant()} 1/2")
                        .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', selectedNames.Take(selectedNames.Count/2))}```")).ConfigureAwait(false);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor().WithTitle($"{type} results for {icao.ToUpperInvariant()} 2/2")
                        .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', selectedNames.Skip(selectedNames.Count/2))}```")).ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor().WithTitle($"{type} results for {icao.ToUpperInvariant()}")
                        .WithDescription($"Use command again with the exact name from below:\n```{desc}```")).ConfigureAwait(false);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await ctx.Channel.SendErrorAsync("Something went wrong, please try again.");
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