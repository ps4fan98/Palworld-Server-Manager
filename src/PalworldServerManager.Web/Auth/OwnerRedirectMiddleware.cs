using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PalworldServerManager.Infrastructure.Identity;

namespace PalworldServerManager.Web.Auth;

public sealed class OwnerRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, UserManager<OwnerUser> users)
    {
        if (HttpMethods.IsGet(context.Request.Method) &&
            !IsAnonymousPath(context.Request.Path) &&
            context.User.Identity?.IsAuthenticated != true &&
            !await users.Users.AnyAsync())
        {
            context.Response.Redirect("/setup");
            return;
        }

        await next(context);
    }

    private static bool IsAnonymousPath(PathString path) =>
        path.StartsWithSegments("/setup") ||
        path.StartsWithSegments("/login") ||
        path.StartsWithSegments("/error") ||
        path.StartsWithSegments("/api") ||
        path.StartsWithSegments("/_framework") ||
        path.StartsWithSegments("/_content") ||
        path.Value?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true ||
        path.Value?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true;
}
