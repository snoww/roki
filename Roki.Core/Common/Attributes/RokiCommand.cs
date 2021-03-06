using System;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RokiCommandAttribute : CommandAttribute
    {
        public RokiCommandAttribute([CallerMemberName] string name = "") : base(Localization.GetCommandData(name.ToLowerInvariant()).Key)
        {
        }
    }
}