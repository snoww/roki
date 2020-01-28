using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Discord.Commands;
using Roki.Core.Services;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UsageAttribute : RemarksAttribute
    {
        public UsageAttribute([CallerMemberName] string memberName = "") : base(GetUsage(memberName))
        {
        }

        public static string GetUsage(string memberName)
        {
            var usage = Localization.LoadCommand(memberName.ToLowerInvariant()).Usage;
            return JsonSerializer.Serialize(usage);
        }
    }
}