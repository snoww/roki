using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using Roki.Common;
using Roki.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace Roki.Extensions
{
    public static class Extensions
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

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
            while (module.Parent != null) module = module.Parent;
            return module;
        }

        public static IEnumerable<Type> LoadFrom(this IServiceCollection collection, Assembly assembly)
        {
            // list of all the types which are added with this method
            var addedTypes = new List<Type>();

            Type[] allTypes;
            try
            {
                // first, get all types in te assembly
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                _log.Warn(ex);
                return Enumerable.Empty<Type>();
            }

            // all types which have INService implementation are services
            // which are supposed to be loaded with this method
            // ignore all interfaces and abstract classes
            var services = new Queue<Type>(allTypes
                .Where(x => x.GetInterfaces().Contains(typeof(IRService))
                            && !x.GetTypeInfo().IsInterface && !x.GetTypeInfo().IsAbstract
                )
                .ToArray());

            // we will just return those types when we're done instantiating them
            addedTypes.AddRange(services);

            // get all interfaces which inherit from INService
            // as we need to also add a service for each one of interfaces
            // so that DI works for them too
            var interfaces = new HashSet<Type>(allTypes
                .Where(x => x.GetInterfaces().Contains(typeof(IRService))
                            && x.GetTypeInfo().IsInterface));

            // keep instantiating until we've instantiated them all
            while (services.Count > 0)
            {
                var serviceType = services.Dequeue(); //get a type i need to add

                if (collection.FirstOrDefault(x => x.ServiceType == serviceType) != null) // if that type is already added, skip
                    continue;

                //also add the same type 
                var interfaceType = interfaces.FirstOrDefault(x => serviceType.GetInterfaces().Contains(x));
                if (interfaceType != null)
                {
                    addedTypes.Add(interfaceType);
                    collection.AddSingleton(interfaceType, serviceType);
                }
                else
                {
                    collection.AddSingleton(serviceType, serviceType);
                }
            }

            return addedTypes;
        }

        public static IMessage DeleteAfter(this IUserMessage msg, int seconds)
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
            return msg;
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

        public static string RealSummary(this CommandInfo info, string prefix)
        {
            return string.Format(info.Summary, prefix);
        }

        public static string RealRemarks(this CommandInfo cmd, string prefix)
        {
            return string.Join(" or ", JsonConvert.DeserializeObject<string[]>(cmd.Remarks).Select(x => Format.Code(string.Format(x, prefix))));
        }

        public static double UnixTimestamp(this DateTime dt)
        {
            return dt.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }
        
        public static MemoryStream ToStream(this Image<Rgba32> img, IImageFormat format = null)
        {
            var imageStream = new MemoryStream();
            if (format?.Name == "GIF")
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

        public static Image<Rgba32> MergePokemonTeam(this IEnumerable<Image<Rgba32>> images)
        {
            var width = images.Sum(image => image.Width);

            var output = new Image<Rgba32>(width-240, 120);
            var curWidth = 0;
            var diff = 0;

            foreach (var image in images)
            {
                output.Mutate(o => o.DrawImage(image, new Point(curWidth - diff, 0), 1f));
                curWidth += image.Width;
                diff += 60;
            }

            return output;
        }

        public static Image<Rgba32> MergeTwoVertical(this Image<Rgba32> image1, Image<Rgba32> image2, out IImageFormat format)
        {
            format = PngFormat.Instance;
            var output = new Image<Rgba32>(image1.Width, image1.Height + image2.Height);
            output.Mutate(o => o
                .DrawImage(image1, new Point(0, 0), 1f)
                .DrawImage(image2, new Point(0, image1.Height), 1f));

            return output;
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
            int frames = images.Max(x => x.Frames.Count);

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
                using (var imgFrame = new Image<Rgba32>(width, height))
                {
                    DrawFrame(imgs, imgFrame, j);

                    var frameToAdd = imgFrame.Frames.RootFrame;
                    canvas.Frames.AddFrame(frameToAdd);
                }
            }
            canvas.Frames.RemoveFrame(0);
            return canvas;
        }
    }
}