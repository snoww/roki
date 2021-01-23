using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private IBrowser _browser;

        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        private string _currentAirport;
        private IPage _page;
        private IPlaywright _playwright;
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
                Task _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), _cancellationToken.Token);
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await DisposeContext();
                }, _cancellationToken.Token);
            }
        }

        public async Task<string> GetSTAR(ICommandContext ctx, string icao, string star)
        {
            using IDisposable typing = ctx.Channel.EnterTypingState();
            if (!CheckExistingCharts(icao, "star"))
            {
                await DownloadAllCharts(ctx, icao);
            }

            if (string.IsNullOrWhiteSpace(star))
            {
                await SendInfo(ctx, icao, "star");
                return null;
            }

            return GetChartName(icao, star);
        }

        public async Task<string> GetSID(ICommandContext ctx, string icao, string sid)
        {
            using IDisposable typing = ctx.Channel.EnterTypingState();
            if (!CheckExistingCharts(icao, "sid"))
            {
                await DownloadAllCharts(ctx, icao);
            }

            if (string.IsNullOrWhiteSpace(sid))
            {
                await SendInfo(ctx, icao, "sid");
                return null;
            }

            return GetChartName(icao, sid);
        }

        public async Task<string> GetAPPR(ICommandContext ctx, string icao, string appr)
        {
            using IDisposable typing = ctx.Channel.EnterTypingState();
            if (!CheckExistingCharts(icao, "appr"))
            {
                await DownloadAllCharts(ctx, icao);
            }

            if (string.IsNullOrWhiteSpace(appr))
            {
                await SendInfo(ctx, icao, "appr");
                return null;
            }

            return GetChartName(icao, appr);
        }

        public async Task<string> GetTAXI(ICommandContext ctx, string icao, string taxi)
        {
            using IDisposable typing = ctx.Channel.EnterTypingState();
            if (!CheckExistingCharts(icao, "taxi"))
            {
                await DownloadAllCharts(ctx, icao);
            }

            if (string.IsNullOrWhiteSpace(taxi))
            {
                await SendInfo(ctx, icao, "taxi");
                return null;
            }

            return GetChartName(icao, taxi);
        }

        private static async Task SendInfo(ICommandContext ctx, string icao, string type)
        {
            icao = icao.ToLowerInvariant();
            string[] desc = await File.ReadAllLinesAsync($"data/charts/{icao}/{type}s.txt");
            if (desc.Contains("none", StringComparer.OrdinalIgnoreCase))
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()}")
                    .WithDescription($"There are no {type.ToUpperInvariant()} charts for {icao.ToUpperInvariant()}")).ConfigureAwait(false);
                return;
            }

            if (desc.Sum(x => x.Length) >= 1990)
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()} 1/2")
                    .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', desc.Take(desc.Length / 2))}```")).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()} 2/2")
                    .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', desc.Skip(desc.Length / 2))}```")
                    .WithFooter("Use .chartupdate ICAO to update chart database")).ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()}")
                    .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', desc)}```")
                    .WithFooter("Use .chartupdate ICAO to update chart database")).ConfigureAwait(false);
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
                IElementHandle selector = await _page.QuerySelectorAsync("css=.mat-focus-indicator.charts-btn.mat-mini-fab.mat-button-base.mat-primary.ng-star-inserted");
                if (selector == null)
                {
                    return false;
                }

                await selector.ClickAsync();
                _currentAirport = icao;
            }

            return true;
        }

        private static bool CheckExistingCharts(string icao, string type)
        {
            return File.Exists($"data/charts/{icao.ToLowerInvariant()}/{type}s.txt");
        }

        public async Task DownloadAllCharts(ICommandContext ctx, string icao)
        {
            icao = icao.ToLowerInvariant();

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

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"Downloading charts for {icao.ToUpperInvariant()}, this might take a while. Hold tight."))
                    .ConfigureAwait(false);

                Directory.CreateDirectory($"data/charts/{icao}");
                // get STARs
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription("Downloading STARs...")).ConfigureAwait(false);
                await _page.WaitForTimeoutAsync(1000);
                await _page.ClickAsync("css=.mat-focus-indicator.clr-star >> text=STAR");
                await _page.WaitForTimeoutAsync(2000);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                HtmlNodeCollection starNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                if (starNodes != null)
                {
                    var stars = new StringBuilder();
                    foreach (HtmlNode htmlNode in starNodes)
                    {
                        string nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                        string idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                        chartIds.TryAdd(nameNode, idNode);
                        stars.AppendLine(nameNode);
                    }

                    await File.WriteAllTextAsync($"data/charts/{icao}/stars.txt", stars.ToString());
                }
                else
                {
                    await File.WriteAllTextAsync($"data/charts/{icao}/stars.txt", "none");
                }

                // get SIDSs
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription("Downloading SIDs...")).ConfigureAwait(false);
                await _page.ClickAsync("css=.mat-focus-indicator.clr-sid >> text=SID");
                await _page.WaitForTimeoutAsync(1500);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                HtmlNodeCollection sidNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                if (sidNodes != null)
                {
                    var sids = new StringBuilder();
                    foreach (HtmlNode htmlNode in sidNodes)
                    {
                        string nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                        string idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                        chartIds.TryAdd(nameNode, idNode);
                        sids.AppendLine(nameNode);
                    }

                    await File.WriteAllTextAsync($"data/charts/{icao}/sids.txt", sids.ToString());
                }
                else
                {
                    await File.WriteAllTextAsync($"data/charts/{icao}/sids.txt", "none");
                }

                // get APPRs
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription("Downloading APPs...")).ConfigureAwait(false);
                await _page.ClickAsync("css=.mat-focus-indicator.clr-app >> text=APP");
                await _page.WaitForTimeoutAsync(1500);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                HtmlNodeCollection apprNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                if (sidNodes != null)
                {
                    var apprs = new StringBuilder();
                    foreach (HtmlNode htmlNode in apprNodes)
                    {
                        HtmlNode idNode = htmlNode.SelectSingleNode("div/div[3]/small");
                        if (idNode == null)
                        {
                            continue;
                        }

                        string nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                        chartIds.TryAdd(nameNode, HtmlEntity.DeEntitize(idNode.InnerText));
                        apprs.AppendLine(nameNode);
                    }

                    await File.WriteAllTextAsync($"data/charts/{icao}/apprs.txt", apprs.ToString());
                }
                else
                {
                    await File.WriteAllTextAsync($"data/charts/{icao}/apprs.txt", "none");
                }

                // get TAXIs
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription("Downloading TAXIs...")).ConfigureAwait(false);
                await _page.ClickAsync("css=.mat-focus-indicator.clr-taxi >> text=TAXI");
                await _page.WaitForTimeoutAsync(1500);
                htmlDoc.LoadHtml(await _page.GetContentAsync());
                HtmlNodeCollection taxiNodes = htmlDoc.DocumentNode.SelectNodes("/html/body/app-root/sidenav/mat-sidenav-container/mat-sidenav/div/charts/mat-list/mat-list-item");
                if (taxiNodes != null)
                {
                    var taxis = new StringBuilder();
                    foreach (HtmlNode htmlNode in taxiNodes)
                    {
                        string nameNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/b").InnerText);
                        string idNode = HtmlEntity.DeEntitize(htmlNode.SelectSingleNode("div/div[3]/small").InnerText);
                        chartIds.TryAdd(nameNode, idNode);
                        taxis.AppendLine(nameNode);
                    }

                    await File.WriteAllTextAsync($"data/charts/{icao}/taxis.txt", taxis.ToString());
                }
                else
                {
                    await File.WriteAllTextAsync($"data/charts/{icao}/taxi.txt", "none");
                }

                JsonElement? element = await _page.EvaluateAsync("window.localStorage.getItem(\"access_token\")");
                string? token = element?.GetString();
                using HttpClient http = _http.CreateClient();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                Directory.CreateDirectory($"data/charts/{icao}");
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription("Saving charts...")).ConfigureAwait(false);
                foreach ((string name, string id) in chartIds)
                {
                    string chartName = new string(Array.FindAll(id.ToArray(), char.IsLetterOrDigit)).ToLower();
                    string imageUrl;
                    if (Regex.IsMatch(chartName, "^\\d+.+\\w+.+\\d$"))
                    {
                        imageUrl = await http.GetStringAsync(
                            $"https://charts.api.navigraph.com/2/airports/{icao.ToUpperInvariant()}/signedurls/{icao.ToLowerInvariant()}{chartName.Substring(1)}_d.png");
                    }
                    else
                    {
                        imageUrl = await http.GetStringAsync($"https://charts.api.navigraph.com/2/airports/{icao.ToUpperInvariant()}/signedurls/{icao.ToLowerInvariant()}{chartName}_d.png");
                    }

                    try
                    {
                        GetImage(imageUrl, GetChartName(icao, name));
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _cancellationToken.Cancel();
                await DisposeContext();
                await ctx.Channel.SendErrorAsync("Something went wrong, please try again.");
                return;
            }
            finally
            {
                _semaphore.Release();
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"Finished saving charts for {icao.ToUpperInvariant()}"));
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