using System;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Roki.Core.Services;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DescriptionAttribute : SummaryAttribute
    {
        public DescriptionAttribute([CallerMemberName] string memberName = "") : base(Localization.LoadCommand(memberName.ToLowerInvariant())
            .Description)
        {
        }
    }
}