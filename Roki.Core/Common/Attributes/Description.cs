using System;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DescriptionAttribute : SummaryAttribute
    {
        public DescriptionAttribute([CallerMemberName] string name = "") : base(Localization.GetCommandData(name.ToLowerInvariant()).Value.Description)
        {
        }
    }
}