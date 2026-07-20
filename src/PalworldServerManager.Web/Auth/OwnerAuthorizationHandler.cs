using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using PalworldServerManager.Infrastructure.Identity;

namespace PalworldServerManager.Web.Auth;

public sealed class OwnerRequirement : IAuthorizationRequirement;

public sealed class OwnerAuthorizationHandler(UserManager<OwnerUser> users)
    : AuthorizationHandler<OwnerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.IsInRole("Owner"))
        {
            return;
        }

        var user = await users.GetUserAsync(context.User);
        if (user?.IsEnabled == true)
        {
            context.Succeed(requirement);
        }
    }
}
