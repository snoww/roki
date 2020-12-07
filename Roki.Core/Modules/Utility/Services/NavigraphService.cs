using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PlaywrightSharp;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Utility.Services
{
    public class NavigraphService : IRokiService
    {
        private readonly IRokiConfig _config;

        private readonly WebClient _webClient;
        private IPlaywright _playwright;
        private IBrowser _browser;
        private IPage _page;
        private string _currentAirport;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public NavigraphService(IRokiConfig config)
        {
            _config = config;
            _webClient = new WebClient();
        }

        private async Task CreateContext()
        {
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
            return await GetChartPage(ctx, icao, "STAR", star);
        }
        
        public async Task<string> GetSID(ICommandContext ctx, string icao, string sid)
        {
            return await GetChartPage(ctx, icao, "SID", sid);
        }

        public async Task<string> GetAPPR(ICommandContext ctx, string icao, string appr)
        {

            return await GetChartPage(ctx, icao, "APP", appr);
        }

        public async Task<string> GetAirport(ICommandContext ctx, string icao)
        {
            using var typing = ctx.Channel.EnterTypingState();
            var chartName = $"data/charts/{icao}-airport.png".ToLowerInvariant();
            if (ChartExists(chartName))
            {
                return chartName;
            }

            try
            {
                await _semaphore.WaitAsync();
                await EnsureContextCreated();
                if (!await SetCurrentAirport(icao))
                {
                    await ctx.Channel.SendErrorAsync("Airport not found");
                    return null;
                }
                
                await _page.WaitForTimeoutAsync(1500);
                await _page.ClickAsync("text=TAXI");
                await _page.ClickAsync("text=-9");
                await _page.WaitForTimeoutAsync(1000);
                var content = await _page.GetContentAsync();
                var index = content.IndexOf("https://airport.charts.api.navigraph.com/raw", StringComparison.OrdinalIgnoreCase);
                var substring = content.Substring(index);
                int end = substring.IndexOf("\"", StringComparison.Ordinal);
                var url = substring.Substring(0, end).Replace("amp;", "");
                return GetImage(url, chartName);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("Something went wrong while trying to get charts. Please try again");
            }
            finally
            {
                _semaphore.Release();
            }
            
            return null;
        }

        private static string GetChartName(string icao, string filename)
        {
            if (filename == null)
            {
                return $"data/charts/{icao}_NA.png".ToLowerInvariant();
            }
            var chars = Array.FindAll(filename.ToArray(), char.IsLetterOrDigit);
            return $"data/charts/{icao}_{new string(chars)}.png".ToLowerInvariant();
        }

        private static bool ChartExists(string filename)
        {
            var path = filename;
            if (!File.Exists(path)) return false;
            var created = File.GetCreationTimeUtc(path);
            if (created - DateTime.UtcNow <= TimeSpan.FromDays(14)) return true;
            File.Delete(path);
            return false;
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

        private async Task<string> GetChartPage(ICommandContext ctx, string icao, string type, string chart)
        {
            using var typing = ctx.Channel.EnterTypingState();
            var filename = GetChartName(icao, chart);
            if (ChartExists(filename))
            {
                return filename;
            }

            try
            {
                await _semaphore.WaitAsync();
                await EnsureContextCreated();
                if (!await SetCurrentAirport(icao))
                {
                    await ctx.Channel.SendErrorAsync("Airport not found");
                    return null;
                }
                await _page.WaitForTimeoutAsync(1500);
                await _page.ClickAsync($"css=.mat-focus-indicator.clr-{type.ToLowerInvariant()} >> text={type}");
                await _page.WaitForTimeoutAsync(1000);

                IElementHandle[] selector;
                switch (type)
                {
                    case "SID":
                    {
                        selector = (await _page.QuerySelectorAllAsync("text=DEP")).ToArray();
                        if (selector.Length == 0)
                        {
                            await ctx.Channel.SendErrorAsync($"No STAR charts found for {icao}");
                            return null;
                        }

                        break;
                    }
                    case "STAR":
                    {
                        selector = (await _page.QuerySelectorAllAsync("text=ARR")).ToArray();
                        if (selector.Length == 0)
                        {
                            await ctx.Channel.SendErrorAsync($"No SID charts found for {icao}");
                            return null;
                        }

                        break;
                    }
                    default:
                    {
                        selector = (await _page.QuerySelectorAllAsync("text=RWY")).ToArray();
                        if (selector.Length == 0)
                        {
                            await ctx.Channel.SendErrorAsync($"No APP charts found for {icao}");
                            return null;
                        }

                        break;
                    }
                }

                var match = false;
                var options = new List<string>();
                foreach (var element in selector)
                {
                    var name = await element.GetInnerTextAsync();
                    // hardcode skip
                    if (name == "arrow_drop_down")
                        continue;
                    if (chart == name)
                    {
                        match = true;
                        await element.ClickAsync();
                        break;
                    }
                    if (options.Contains(name, StringComparer.Ordinal))
                    {
                        break;
                    }
                    options.Add(name);
                }
            
                if (!match)
                {
                    if (options.Count == 0)
                    {
                        await ctx.Channel.SendErrorAsync($"No APP charts found for {icao}, please try again if there should eb charts.");
                        return null;
                    }
                    await ctx.Channel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor().WithTitle($"{type} results for {icao}")
                        .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', options)}```")).ConfigureAwait(false);
                    return null;
                }
            
                string content = await _page.GetContentAsync();
                var index = content.IndexOf("https://airport.charts.api.navigraph.com/raw", StringComparison.OrdinalIgnoreCase);
                var substring = content.Substring(index);
                int end = substring.IndexOf("\"", StringComparison.Ordinal);
                var url = substring.Substring(0, end).Replace("amp;", "");
                return GetImage(url, filename);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("Something went wrong while trying to get charts. Please try again");
            }
            finally
            {
                _semaphore.Release();
            }
            
            return null;
        }

        private string GetImage(string url, string filename)
        {
            _webClient.DownloadFile(url, filename);
            return filename;
        }

        private async Task DisposeContext()
        {
            await _page.CloseAsync();
            await _browser.CloseAsync();
            _playwright.Dispose();
        }
    }
}