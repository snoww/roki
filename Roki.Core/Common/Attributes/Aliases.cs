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
        public AliasesAttribute([CallerMemberName] string memberName = "") : base(Localization.LoadCommand(memberName.ToLowerInvariant()).Command
            .Split(' ').Skip(1).ToArray())
        {
        }
    }
}