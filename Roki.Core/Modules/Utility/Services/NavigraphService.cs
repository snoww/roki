using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Utility.Services
{
    public class NavigraphService : IRokiService
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);

        public async Task<string> GetSTAR(ICommandContext ctx, string icao, string star)
        {
            using IDisposable typing = ctx.Channel.EnterTypingState();
            await Semaphore.WaitAsync();
            if (!CheckExistingCharts(icao, "star"))
            {
                int exit = await GetChartsAsync(ctx, icao);
                if (exit != 0)
                {
                    await SendError(ctx, icao, exit);
                }
            }
            Semaphore.Release();

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
            await Semaphore.WaitAsync();
            if (!CheckExistingCharts(icao, "sid"))
            {
                int exit = await GetChartsAsync(ctx, icao);
                if (exit != 0)
                {
                    await SendError(ctx, icao, exit);
                }
            }
            Semaphore.Release();

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
            await Semaphore.WaitAsync();
            if (!CheckExistingCharts(icao, "appr"))
            {
                int exit = await GetChartsAsync(ctx, icao);
                if (exit != 0)
                {
                    await SendError(ctx, icao, exit);
                }
            }
            Semaphore.Release();

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
            await Semaphore.WaitAsync();
            if (!CheckExistingCharts(icao, "taxi"))
            {
                int exit = await GetChartsAsync(ctx, icao);
                if (exit != 0)
                {
                    await SendError(ctx, icao, exit);
                }
            }
            Semaphore.Release();

            if (string.IsNullOrWhiteSpace(taxi))
            {
                await SendInfo(ctx, icao, "taxi");
                return null;
            }

            return GetChartName(icao, taxi);
        }

        public async Task<int> GetChartsAsync(ICommandContext ctx, string icao)
        {
            using var proc = new Process
            {
                StartInfo =
                {
                    FileName = "/usr/bin/dotnet",
                    Arguments = $"/home/snow/Documents/navigraph/navigraph.dll {icao} data/charts",
                    UseShellExecute = false
                }
            };
            proc.Start();
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"Retrieving charts for {icao.ToUpperInvariant()} ...\nPlease be patient."));
            await proc.WaitForExitAsync();
            return proc.ExitCode;
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
                    .WithFooter("To update: .chartupdate ICAO")).ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor().WithTitle($"{type.ToUpperInvariant()} results for {icao.ToUpperInvariant()}")
                    .WithDescription($"Use command again with the exact name from below:\n```{string.Join('\n', desc)}```")
                    .WithFooter("To update: .chartupdate ICAO")).ConfigureAwait(false);
            }
        }

        private static async Task SendError(ICommandContext ctx, string icao, int errorCode)
        {
            if (errorCode == -1)
            {
                await ctx.Channel.SendErrorAsync("Invalid ICAO provided. Please make sure you have the correct ICAO");
            }
            else if (errorCode == -2)
            {
                await ctx.Channel.SendErrorAsync($"Something went wrong when trying to retrieve charts for {icao.ToUpperInvariant()}, please try again");
            }
        }

        private static string GetChartName(string icao, string filename)
        {
            return $"data/charts/{icao}/{icao}_{new string(Array.FindAll(filename.ToArray(), char.IsLetterOrDigit))}.png".ToLowerInvariant();
        }

        private static bool CheckExistingCharts(string icao, string type)
        {
            return File.Exists($"data/charts/{icao.ToLowerInvariant()}/{type}s.txt");
        }
    }
}