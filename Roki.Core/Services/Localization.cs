using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Roki.Common;

namespace Roki.Core.Services
{
    public class Localization
    {
        private static readonly Dictionary<string, CommandData> CommandData =
            JsonSerializer.Deserialize<Dictionary<string, CommandData>>(File.ReadAllText("./_strings/command_strings.json"));

        public static CommandData LoadCommand(string key)
        {
            CommandData.TryGetValue(key, out var toReturn);

            if (toReturn == null)
                return new CommandData
                {
                    Command = key,
                    Description = key,
                    Usage = new[] {key}
                };

            return toReturn;
        }
    }
}