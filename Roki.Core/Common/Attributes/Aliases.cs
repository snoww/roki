using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Roki.Core.Services;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AliasesAttribute : AliasAttribute
    {
        public AliasesAttribute([CallerMemberName] string name = "") : base(Localization.GetCommandData(name.ToLowerInvariant()).Command
            .Split(' ').Skip(1).ToArray())
        {
        }
    }
}