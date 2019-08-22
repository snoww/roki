using System;
using System.Collections.Generic;
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
                .Where(x => x.GetInterfaces().Contains(typeof(INService))
                            && !x.GetTypeInfo().IsInterface && !x.GetTypeInfo().IsAbstract
                )
                .ToArray());

            // we will just return those types when we're done instantiating them
            addedTypes.AddRange(services);

            // get all interfaces which inherit from INService
            // as we need to also add a service for each one of interfaces
            // so that DI works for them too
            var interfaces = new HashSet<Type>(allTypes
                .Where(x => x.GetInterfaces().Contains(typeof(INService))
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

        public static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int? lastPage)
        {
            if (lastPage != null)
                return embed.WithFooter(efb => efb.WithText($"{curPage + 1} / {lastPage + 1}"));
            return embed.WithFooter(efb => efb.WithText(curPage.ToString()));
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
    }
}