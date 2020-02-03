using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AliasesAttribute : AliasAttribute
    {
        public AliasesAttribute([CallerMemberName] string name = "") : base(GetAliases(name))
        {
        }

        private static string[] GetAliases(string name)
        {
            var aliases = Localization.GetCommandData(name.ToLowerInvariant()).Value.Aliases;
            return string.IsNullOrWhiteSpace(aliases) ? new string[]{} : aliases.Split();
        }
    }
}