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
using MongoDB.Bson;
using NLog;
using Roki.Common;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using StackExchange.Redis;

namespace Roki.Extensions
{
    public static class Extensions
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Random Rng = new Random();
        private static readonly IDatabase Cache = new RedisCache().Redis.GetDatabase();
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};

        public static EmbedBuilder WithDynamicColor(this EmbedBuilder embed, ICommandContext context)
        {
            var color = (uint) Cache.StringGet($"color:{context.Guild.Id}");
            return embed.WithColor(new Discord.Color(color));
        }
        
        public static EmbedBuilder WithDynamicColor(this EmbedBuilder embed, ulong guildId)
        {
            var color = (uint) Cache.StringGet($"color:{guildId}");
            return embed.WithColor(new Discord.Color(color));
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

        public static void LoadFrom(this IServiceCollection collection, Assembly assembly)
        {
            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Log.Warn(e);
                return;
            }
            
            var services = new Queue<Type>(allTypes.Where(x => 
                x.GetInterfaces().Contains(typeof(IRokiService)) && !x.GetTypeInfo().IsInterface && !x.GetTypeInfo().IsAbstract));

            var interfaces = new HashSet<Type>(allTypes.Where(x => 
                x.GetInterfaces().Contains(typeof(IRokiService)) && x.GetTypeInfo().IsInterface));
            
            while (services.Count > 0)
            {
                var serviceType = services.Dequeue();

                if (collection.FirstOrDefault(x => x.ServiceType == serviceType) != null)
                    continue;

                var interfaceType = interfaces.FirstOrDefault(x => serviceType.GetInterfaces().Contains(x));
                collection.AddSingleton(interfaceType != null ? interfaceType : serviceType, serviceType);
            }
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
            if (reactionRemoved == null)
                reactionRemoved = _ => Task.CompletedTask;

            var wrap = new ReactionEventWrapper(client, msg);
            wrap.OnReactionAdded += r =>
            {
                var _ = Task.Run(() => reactionAdded(r));
            };
            wrap.OnReactionRemoved += r =>
            {
                var _ = Task.Run(() => reactionRemoved(r));
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

        public static string FormatRemarks(this CommandInfo command, string prefix)
        {
            return string.Join("\n", command.Remarks.Deserialize<string[]>().Select(x => Format.Code(string.Format(x, prefix))));
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
                img.SaveAsPng(imageStream, new PngEncoder { CompressionLevel = 9 });
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
                foreach (var t in imgArray)
                {
                    var frame = t.Frames.CloneFrame(frameNumber % t.Frames.Count);
                    imgFrame.Mutate(x => x.DrawImage(frame, new Point(xOffset, 0), GraphicsOptions.Default));
                    xOffset += t.Bounds().Width;
                }
            }

            var imgs = images.ToArray();
            
            var frames = imgs.Max(x => x.Frames.Count);
            var width = imgs.Sum(img => img.Width);
            var height = imgs.Max(img => img.Height);
            var canvas = new Image<Rgba32>(width, height);
            if (frames == 1)
            {
                DrawFrame(imgs, canvas, 0);
                return canvas;
            }

            format = GifFormat.Instance;
            for (int j = 0; j < frames; j++)
            {
                using var imgFrame = new Image<Rgba32>(width, height);
                DrawFrame(imgs, imgFrame, j);

                var frameToAdd = imgFrame.Frames.RootFrame;
                canvas.Frames.AddFrame(frameToAdd);
            }
            canvas.Frames.RemoveFrame(0);
            return canvas;
        }
        
        public static void Shuffle<T>(this IList<T> list)  
        {  
            var n = list.Count;  
            while (n > 1) {  
                n--;  
                var k = Rng.Next(n + 1);
                var value = list[k];  
                list[k] = list[n];  
                list[n] = value;  
            }  
        }
        
        public static string ToReadableString(this TimeSpan span, string separator = ", ")
        {
            var formatted =
                $"{(span.Duration().Days >= 7 ? $"{span.Days / 7} week{(span.Days / 7 == 1 ? string.Empty : "s")}{separator}" : string.Empty)}" +
                $"{(span.Duration().Days % 7 > 0 ? $"{span.Days % 7} day{(span.Days % 7 == 1 ? string.Empty : "s")}{separator}" : string.Empty)}" +
                $"{(span.Duration().Hours > 0 ? $"{span.Hours:0} hour{(span.Hours == 1 ? string.Empty : "s")}{separator}" : string.Empty)}" +
                $"{(span.Duration().Minutes > 0 ? $"{span.Minutes:0} minute{(span.Minutes == 1 ? string.Empty : "s")}{separator}" : string.Empty)}";

            if (formatted.EndsWith(", ")) 
                formatted = formatted.Substring(0, formatted.Length - 2);

            if (string.IsNullOrEmpty(formatted)) 
                formatted = "0 seconds";

            return formatted;
        }
        
        public static T Deserialize<T>(this string json)
        {       
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        public static int GetId(this ObjectId id, int digits = 4)
        {
            return id.Increment % (int) Math.Pow(10, digits);
        }
    }
}