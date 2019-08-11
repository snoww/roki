using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Roki.Common;

namespace Roki.Core.Services
{
    public class Localization
    {
        private static readonly Dictionary<string, CommandData> CommandData = JsonConvert.DeserializeObject<Dictionary<string, CommandData>>(File.ReadAllText("./_strings/cmd/command_strings.json"));
        
        public static CommandData LoadCommand(string key)
        {
            CommandData.TryGetValue(key, out var toReturn);

            if (toReturn == null)
                return new CommandData
                {
                    Command = key,
                    Description = key,
                    Usage = new[] { key },
                };

            return toReturn;
        }


    }
    
}