using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Roki.Web.Services;

namespace Roki.Web.Middleware
{
    public static class AuthenticateGuildMiddlewareExtensions
    {
        public static IApplicationBuilder AuthenticateGuild(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticateGuildMiddleware>();
        }
    }
    
    public class AuthenticateGuildMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthenticateGuildMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        
        public async Task InvokeAsync(HttpContext context, RokiContext dbContext)
        {
            if (!context.Request.Path.StartsWithSegments("/manage"))
            {
                await _next(context);
                return;
            }
            
            string[] route = context.Request.Path.ToString().Split('/');
            if (route.Length < 3)
            {
                await _next(context);
                return;
            }

            if (!ulong.TryParse(route[2], out ulong guildId))
            {
                context.Response.Redirect("/manage");
                await context.Response.CompleteAsync();
                return;
            }
            
            ulong userId = ulong.Parse(context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"));
            if (await dbContext.Guilds.AsNoTracking().AnyAsync(x => x.Id == guildId && (x.OwnerId == userId || x.Moderators.Contains((long) userId))))
            {
                await _next(context);
                return;
            }

            context.Response.Redirect("/manage");
            await context.Response.CompleteAsync();
        }
    }
}