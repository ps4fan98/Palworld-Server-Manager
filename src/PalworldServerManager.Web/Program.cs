using PalworldServerManager.Core.Services;
using PalworldServerManager.Infrastructure;
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

builder.Services.AddPalworldManagerInfrastructure(
    builder.Configuration,
    builder.Environment);

var app = builder.Build();

await app.Services.InitializePalworldManagerInfrastructureAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Palworld Server Manager",
    observedAtUtc = DateTimeOffset.UtcNow
}));

app.MapGet(
    "/api/v1/server/status",
    async (
        IPalworldProcessService processService,
        CancellationToken cancellationToken) =>
    {
        var status =
            await processService.GetStatusAsync(cancellationToken);

        return Results.Ok(status);
    });

app.MapGet(
    "/api/v1/server/logs",
    async (
        int? take,
        IPalworldProcessService processService,
        CancellationToken cancellationToken) =>
    {
        var logs = await processService.GetRecentLogsAsync(
            take ?? 100,
            cancellationToken);

        return Results.Ok(logs);
    });

app.MapPost(
    "/api/v1/server/start",
    async (
        IPalworldProcessService processService,
        IAuditWriter auditWriter,
        CancellationToken cancellationToken) =>
    {
        var result = await processService.StartAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "server.start",
            result.Success ? "success" : "failure",
            result.Message,
            cancellationToken: cancellationToken);

        return result.Success
            ? Results.Ok(result)
            : Results.Conflict(result);
    });

app.MapPost(
    "/api/v1/server/force-stop",
    async (
        IPalworldProcessService processService,
        IAuditWriter auditWriter,
        CancellationToken cancellationToken) =>
    {
        var result =
            await processService.ForceStopAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "server.force-stop",
            result.Success ? "success" : "failure",
            result.Message,
            cancellationToken: cancellationToken);

        return result.Success
            ? Results.Ok(result)
            : Results.Conflict(result);
    });

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
