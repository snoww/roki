using System;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Roki.Core.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RokiCommandAttribute : CommandAttribute
    {
        public RokiCommandAttribute([CallerMemberName] string memberName = "") : base(Localization.LoadCommand(memberName.ToLowerInvariant()).Command
            .Split(' ')[0])
        {
        }
    }
}