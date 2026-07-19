using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using PalworldServerManager.Core.Services;
using PalworldServerManager.Infrastructure;
using PalworldServerManager.Infrastructure.Identity;
using PalworldServerManager.Infrastructure.Persistence;
using PalworldServerManager.Web.Auth;
using PalworldServerManager.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Palworld Server Manager";
});

var listenUrl =
    builder.Configuration["Manager:ListenUrl"] ??
    "http://127.0.0.1:8213";

builder.WebHost.UseUrls(listenUrl);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddPalworldManagerInfrastructure(
    builder.Configuration,
    builder.Environment);

builder.Services.AddIdentity<OwnerUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ManagerDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "PalworldServerManager.Owner";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context => ApiStatusOrRedirect(context, StatusCodes.Status401Unauthorized),
        OnRedirectToAccessDenied = context => ApiStatusOrRedirect(context, StatusCodes.Status403Forbidden)
    };
});

builder.Services.AddScoped<IAuthorizationHandler, OwnerAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Owner", policy => policy.AddRequirements(new OwnerRequirement()));
});

var app = builder.Build();
var setupGate = new SemaphoreSlim(1, 1);

await app.Services.InitializePalworldManagerInfrastructureAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OwnerRedirectMiddleware>();
app.UseAntiforgery();

app.MapStaticAssets();


app.MapGet("/setup", async (HttpContext http, UserManager<OwnerUser> users, IAntiforgery antiforgery) =>
{
    if (await users.Users.AnyAsync())
    {
        return Results.Redirect("/login");
    }

    var token = antiforgery.GetAndStoreTokens(http).RequestToken;
    return Results.Content(AuthPage("First-run owner setup", "/setup", token, "Create owner", includeConfirmation: true), "text/html");
}).AllowAnonymous();

app.MapPost("/setup", async (
    HttpContext http,
    UserManager<OwnerUser> users,
    SignInManager<OwnerUser> signIn,
    RoleManager<IdentityRole> roles,
    IAuditWriter audit,
    IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync();
    var username = form["Username"].ToString().Trim();
    var password = form["Password"].ToString();
    var confirmation = form["ConfirmPassword"].ToString();
    if (password != confirmation)
    {
        return Results.Content(AuthPage("First-run owner setup", "/setup", null, "Create owner", "Password confirmation does not match.", true), "text/html", statusCode: 400);
    }

    await setupGate.WaitAsync();
    try
    {
        if (await users.Users.AnyAsync())
        {
            return Results.Conflict("An owner account already exists.");
        }

        if (!await roles.RoleExistsAsync("Owner"))
        {
            await roles.CreateAsync(new IdentityRole("Owner"));
        }

        var user = new OwnerUser { UserName = username, CreatedAtUtc = DateTimeOffset.UtcNow, IsEnabled = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var message = string.Join(" ", result.Errors.Select(error => WebUtility.HtmlEncode(error.Description)));
            return Results.Content(AuthPage("First-run owner setup", "/setup", null, "Create owner", message, true), "text/html", statusCode: 400);
        }

        await users.AddToRoleAsync(user, "Owner");
        await audit.WriteAsync("auth.owner-created", "success", $"username={username}; sourceIp={http.Connection.RemoteIpAddress}", username);
        await signIn.SignInAsync(user, isPersistent: false);
        return Results.Redirect("/");
    }
    finally
    {
        setupGate.Release();
    }
}).AllowAnonymous();

app.MapGet("/login", async (HttpContext http, UserManager<OwnerUser> users, IAntiforgery antiforgery) =>
{
    if (http.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect(SafeReturnUrl(http.Request.Query["returnUrl"].ToString()) ?? "/");
    }

    if (!await users.Users.AnyAsync())
    {
        return Results.Redirect("/setup");
    }

    var token = antiforgery.GetAndStoreTokens(http).RequestToken;
    return Results.Content(AuthPage("Owner login", "/login", token, "Sign in", returnUrl: SafeReturnUrl(http.Request.Query["returnUrl"].ToString())), "text/html");
}).AllowAnonymous();

app.MapPost("/login", async (
    HttpContext http,
    UserManager<OwnerUser> users,
    SignInManager<OwnerUser> signIn,
    IAuditWriter audit,
    IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync();
    var username = form["Username"].ToString().Trim();
    var returnUrl = SafeReturnUrl(form["ReturnUrl"].ToString()) ?? "/";
    var user = await users.FindByNameAsync(username);
    const string generic = "Invalid username or password.";
    if (user?.IsEnabled != true)
    {
        await audit.WriteAsync("auth.login-failure", "failure", $"username={username}; category=invalid; sourceIp={http.Connection.RemoteIpAddress}", "anonymous");
        return Results.Content(AuthPage("Owner login", "/login", null, "Sign in", generic, returnUrl: returnUrl), "text/html", statusCode: 400);
    }

    var result = await signIn.PasswordSignInAsync(user, form["Password"].ToString(), isPersistent: false, lockoutOnFailure: true);
    if (!result.Succeeded)
    {
        await audit.WriteAsync(result.IsLockedOut ? "auth.account-locked" : "auth.login-failure", "failure", $"username={username}; category={(result.IsLockedOut ? "locked" : "invalid")}; sourceIp={http.Connection.RemoteIpAddress}", "anonymous");
        return Results.Content(AuthPage("Owner login", "/login", null, "Sign in", generic, returnUrl: returnUrl), "text/html", statusCode: 400);
    }

    user.LastSuccessfulLoginAtUtc = DateTimeOffset.UtcNow;
    await users.UpdateAsync(user);
    await audit.WriteAsync("auth.login-success", "success", $"username={username}; sourceIp={http.Connection.RemoteIpAddress}", username);
    return Results.Redirect(returnUrl);
}).AllowAnonymous();

app.MapPost("/logout", async (HttpContext http, SignInManager<OwnerUser> signIn, IAuditWriter audit, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(http);
    await audit.WriteAsync("auth.logout", "success", $"username={http.User.Identity?.Name}; sourceIp={http.Connection.RemoteIpAddress}", http.User.Identity?.Name ?? "anonymous");
    await signIn.SignOutAsync();
    return Results.Redirect("/login");
}).RequireAuthorization("Owner");

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Palworld Server Manager",
    observedAtUtc = DateTimeOffset.UtcNow
})).AllowAnonymous();

var serverApi = app.MapGroup("/api/v1/server").RequireAuthorization("Owner");

serverApi.MapGet(
    "/status",
    async (IPalworldProcessService processService, CancellationToken cancellationToken) =>
        Results.Ok(await processService.GetStatusAsync(cancellationToken)));

serverApi.MapGet(
    "/logs",
    async (int? take, IPalworldProcessService processService, CancellationToken cancellationToken) =>
        Results.Ok(await processService.GetRecentLogsAsync(take ?? 100, cancellationToken)));

serverApi.MapPost(
    "/start",
    async (IPalworldProcessService processService, IAuditWriter auditWriter, CancellationToken cancellationToken) =>
    {
        var result = await processService.StartAsync(cancellationToken);
        await auditWriter.WriteAsync("server.start", result.Success ? "success" : "failure", result.Message, cancellationToken: cancellationToken);
        return result.Success ? Results.Ok(result) : Results.Conflict(result);
    });

serverApi.MapPost(
    "/force-stop",
    async (IPalworldProcessService processService, IAuditWriter auditWriter, CancellationToken cancellationToken) =>
    {
        var result = await processService.ForceStopAsync(cancellationToken);
        await auditWriter.WriteAsync("server.force-stop", result.Success ? "success" : "failure", result.Message, cancellationToken: cancellationToken);
        return result.Success ? Results.Ok(result) : Results.Conflict(result);
    });

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static Task ApiStatusOrRedirect(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = statusCode;
    }
    else
    {
        context.Response.Redirect(context.RedirectUri);
    }

    return Task.CompletedTask;
}

static string? SafeReturnUrl(string? returnUrl) =>
    !string.IsNullOrWhiteSpace(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out var uri) && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        ? uri.ToString()
        : null;

static string AuthPage(string title, string action, string? token, string button, string? error = null, bool includeConfirmation = false, string? returnUrl = null) =>
    $$"""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><link rel="stylesheet" href="/app.css"><title>{{title}}</title></head>
<body><main class="auth-page"><section class="auth-card"><h1>{{title}}</h1><p>Palworld Server Manager remains localhost-only. There are no default credentials or public registration.</p>
{{(string.IsNullOrWhiteSpace(error) ? "" : $"<div class=\"operation-message failure\">{error}</div>")}}
<form method="post" action="{{action}}"><input name="__RequestVerificationToken" type="hidden" value="{{WebUtility.HtmlEncode(token)}}"><input name="ReturnUrl" type="hidden" value="{{WebUtility.HtmlEncode(returnUrl)}}"><label>Username<input required maxlength="256" name="Username" autocomplete="username"></label><label>Password<input required name="Password" type="password" autocomplete="current-password"></label>{{(includeConfirmation ? "<label>Confirm password<input required name=\"ConfirmPassword\" type=\"password\" autocomplete=\"new-password\"></label>" : "")}}<button class="button primary" type="submit">{{button}}</button></form></section></main></body></html>
""";

public partial class Program;
