using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Discord.Commands;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UsageAttribute : RemarksAttribute
    {
        public UsageAttribute([CallerMemberName] string name = "") : base(JsonSerializer.Serialize(Localization.GetCommandData(name.ToLowerInvariant()).Value.Usage))
        {
        }
    }
}