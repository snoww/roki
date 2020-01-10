using System.Collections.Generic;
using System.IO;
using Roki.Common;
using Roki.Extensions;

namespace Roki.Core.Services
{
    public class Localization
    {
        private static readonly Dictionary<string, CommandData> CommandData =
            File.ReadAllText("./_strings/command_strings.json").Deserialize<Dictionary<string, CommandData>>();

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