using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Common;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using StackExchange.Redis;
using Color = Discord.Color;

namespace Roki.Extensions
{
    public static class Extensions
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Random Rng = new();
        private static readonly IDatabase Cache = new RedisCache().Redis.GetDatabase();
        private static readonly JsonSerializerOptions Options = new() {PropertyNameCaseInsensitive = true};

        public static EmbedBuilder WithDynamicColor(this EmbedBuilder embed, ICommandContext context)
        {
            return context.Channel is IDMChannel ? embed.WithOkColor() : embed.WithDynamicColor(context.Guild.Id);
        }

        public static EmbedBuilder WithDynamicColor(this EmbedBuilder embed, ulong guildId)
        {
            RedisValue color = Cache.StringGet($"color:{guildId}");
            return embed.WithColor(color.IsNull ? new Color(Roki.OkColor) : new Color((uint) color));
        }

        public static EmbedBuilder WithOkColor(this EmbedBuilder embed)
        {
            return embed.WithColor(Roki.OkColor);
        }

        public static EmbedBuilder WithErrorColor(this EmbedBuilder embed)
        {
            return embed.WithColor(Roki.ErrorColor);
        }

        public static ModuleInfo GetTopLevelModule(this ModuleInfo module)
        {
            while (module.Parent != null)
                module = module.Parent;
            return module;
        }

        public static IServiceCollection LoadFrom(this IServiceCollection collection, Assembly assembly)
        {
            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Log.Warn(e);
                return collection;
            }

            var services = new Queue<Type>(allTypes.Where(x =>
                x.GetInterfaces().Contains(typeof(IRokiService)) && !x.GetTypeInfo().IsInterface && !x.GetTypeInfo().IsAbstract));

            var interfaces = new HashSet<Type>(allTypes.Where(x =>
                x.GetInterfaces().Contains(typeof(IRokiService)) && x.GetTypeInfo().IsInterface));

            while (services.Count > 0)
            {
                Type serviceType = services.Dequeue();

                if (collection.FirstOrDefault(x => x.ServiceType == serviceType) != null)
                {
                    continue;
                }

                Type interfaceType = interfaces.FirstOrDefault(x => serviceType.GetInterfaces().Contains(x));
                collection.AddSingleton(interfaceType ?? serviceType, serviceType);
            }

            return collection;
        }

        public static void DeleteAfter(this IUserMessage msg, int seconds)
        {
            Task.Run(async () =>
            {
                await Task.Delay(seconds * 1000).ConfigureAwait(false);
                try
                {
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    //
                }
            });
        }

        public static ReactionEventWrapper OnReaction(this IUserMessage msg, DiscordSocketClient client, Func<SocketReaction, Task> reactionAdded,
            Func<SocketReaction, Task> reactionRemoved = null)
        {
            reactionRemoved ??= _ => Task.CompletedTask;

            var wrap = new ReactionEventWrapper(client, msg);
            wrap.OnReactionAdded += r =>
            {
                Task _ = Task.Run(() => reactionAdded(r));
            };
            wrap.OnReactionRemoved += r =>
            {
                Task _ = Task.Run(() => reactionRemoved(r));
            };
            return wrap;
        }

        public static IEnumerable<IRole> GetRoles(this IGuildUser user)
        {
            return user.RoleIds.Select(r => user.Guild.GetRole(r)).Where(r => r != null);
        }

        public static string FormatSummary(this CommandInfo command, string prefix)
        {
            return string.Format(command.Summary, prefix);
        }

        public static Stream ToStream(this Image<Rgba32> img, IImageFormat format = null)
        {
            var imageStream = new MemoryStream();

            if (format == GifFormat.Instance)
            {
                img.SaveAsGif(imageStream);
            }
            else
            {
                img.SaveAsPng(imageStream, new PngEncoder {CompressionLevel = PngCompressionLevel.BestCompression});
            }

            imageStream.Position = 0;
            return imageStream;
        }

        public static Image<Rgba32> Merge(this IEnumerable<Image<Rgba32>> images)
        {
            return images.Merge(out _);
        }

        public static Image<Rgba32> Merge(this IEnumerable<Image<Rgba32>> images, out IImageFormat format)
        {
            format = PngFormat.Instance;

            void DrawFrame(Image<Rgba32>[] imgArray, Image<Rgba32> imgFrame, int frameNumber)
            {
                var xOffset = 0;
                var options = new GraphicsOptions();
                foreach (Image<Rgba32> t in imgArray)
                {
                    Image<Rgba32> frame = t.Frames.CloneFrame(frameNumber % t.Frames.Count);
                    imgFrame.Mutate(x => x.DrawImage(frame, new Point(xOffset, 0), options));
                    xOffset += t.Bounds().Width;
                }
            }

            Image<Rgba32>[] imgs = images.ToArray();

            int frames = imgs.Max(x => x.Frames.Count);
            int width = imgs.Sum(img => img.Width);
            int height = imgs.Max(img => img.Height);
            var canvas = new Image<Rgba32>(width, height);
            if (frames == 1)
            {
                DrawFrame(imgs, canvas, 0);
                return canvas;
            }

            format = GifFormat.Instance;
            for (var j = 0; j < frames; j++)
            {
                using var imgFrame = new Image<Rgba32>(width, height);
                DrawFrame(imgs, imgFrame, j);

                ImageFrame<Rgba32> frameToAdd = imgFrame.Frames.RootFrame;
                canvas.Frames.AddFrame(frameToAdd);
            }

            canvas.Frames.RemoveFrame(0);
            return canvas;
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static string ToReadableString(this TimeSpan span, string separator = ", ")
        {
            string formatted =
                $"{(span.Duration().Days >= 7 ? $"{span.Days / 7} week{(span.Days / 7 == 1 ? string.Empty : "s")}{separator}" : string.Empty)}" +
                $"{(span.Duration().Days % 7 > 0 ? $"{span.Days % 7} day{(span.Days % 7 == 1 ? string.Empty : "s")}{separator}" : string.Empty)}" +
                $"{(span.Duration().Hours > 0 ? $"{span.Hours:0} hour{(span.Hours == 1 ? string.Empty : "s")}{separator}" : string.Empty)}" +
                $"{(span.Duration().Minutes > 0 ? $"{span.Minutes:0} minute{(span.Minutes == 1 ? string.Empty : "s")}{separator}" : string.Empty)}";

            if (formatted.EndsWith(", "))
            {
                formatted = formatted.Substring(0, formatted.Length - 2);
            }

            if (string.IsNullOrEmpty(formatted))
            {
                formatted = "0 seconds";
            }

            return formatted;
        }

        public static T Deserialize<T>(this string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        public static string ToSubGuid(this Guid guid)
        {
            return guid.ToString().Substring(0, 7);
        }
    }
}