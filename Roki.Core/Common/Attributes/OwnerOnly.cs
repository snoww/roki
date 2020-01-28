using System;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Roki.Services;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class OwnerOnly : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = services.GetService<IRokiConfig>();

            return Task.FromResult(config.IsOwner(context.User) || context.Client.CurrentUser.Id == context.User.Id
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("Not owner"));
        }
    }
}