using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Core.Services;

namespace Roki.Core.Extentions
{
    public static class Extentions
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();
        
        public static ModuleInfo GetTopLevelModule(this ModuleInfo module)
        {
            while (module.Parent != null)
            {
                module = module.Parent;
            }
            return module;
        }

        
        public static IEnumerable<Type> LoadFrom(this IServiceCollection collection, Assembly assembly)
        {
            // list of all the types which are added with this method
            List<Type> addedTypes = new List<Type>();

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

    }
}