using System.Collections.Generic;
using System.IO;
using Roki.Common;
using Roki.Extensions;

namespace Roki.Services
{
    public class Localization
    {
        private static readonly Dictionary<string, CommandData> CommandData =
            File.ReadAllText("./data/command_data.json").Deserialize<Dictionary<string, CommandData>>();

        public static CommandData GetCommandData(string key)
        {
            CommandData.TryGetValue(key, out var data);

            if (data == null)
                return new CommandData
                {
                    Command = key,
                    Description = key,
                    Usage = new[] {key}
                };

            return data;
        }
    }
}