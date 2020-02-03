using System.Collections.Generic;
using System.IO;
using Roki.Common;
using Roki.Extensions;

namespace Roki.Services
{
    public class Localization
    {
        private static readonly Dictionary<string, CommandData> CommandData = File.ReadAllText("./data/command_data.json").Deserialize<Dictionary<string, CommandData>>();

        public static KeyValuePair<string, CommandData> GetCommandData(string key)
        {
            CommandData.TryGetValue(key, out var data);
            
            return data == null 
                ? new KeyValuePair<string, CommandData>(key, new CommandData {Description = key, Usage = new[] {key}}) 
                : new KeyValuePair<string, CommandData>(key, data);
        }
    }
}