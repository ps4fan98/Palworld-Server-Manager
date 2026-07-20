using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PalworldServerManager.Infrastructure;
using PalworldServerManager.Infrastructure.Identity;
using PalworldServerManager.Infrastructure.Persistence;
using Xunit;

namespace PalworldServerManager.Web.Tests;

public sealed class AuthenticationFlowTests
{
    [Fact] public async Task HealthEndpointRemainsAnonymous() { await using var f = new ManagerFactory(); using var c = f.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/health")).StatusCode); }
    [Fact] public async Task AnonymousStatusReturnsUnauthorized() => await AssertAnonymousGet("/api/v1/server/status");
    [Fact] public async Task AnonymousLogsReturnsUnauthorized() => await AssertAnonymousGet("/api/v1/server/logs");
    [Fact] public async Task AnonymousStartReturnsUnauthorized() => await AssertAnonymousPost("/api/v1/server/start");
    [Fact] public async Task AnonymousForceStopReturnsUnauthorized() => await AssertAnonymousPost("/api/v1/server/force-stop");

    [Fact]
    public async Task FreshUiRequestRedirectsToSetup()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        var r = await c.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/setup", r.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LoginRedirectsToSetupBeforeOwnerExists()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        var r = await c.GetAsync("/login");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/setup", r.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task FirstOwnerSetupSucceedsAndAuthenticatesSession()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        var r = await SetupOwner(c);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/", r.Headers.Location?.OriginalString);
        Assert.NotEqual(HttpStatusCode.Redirect, (await c.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task SecondOwnerCannotBeCreated()
    {
        await using var f = new ManagerFactory();
        using var c1 = f.CreateClient(new() { AllowAutoRedirect = false });
        using var c2 = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c1);
        var token = await GetToken(c2, "/setup");
        var r = await c2.PostAsync("/setup", Form(token, "second", "ValidPass123!", "ValidPass123!"));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task SetupUnavailableAfterOwnerExists()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c);
        var r = await c.GetAsync("/setup");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/login", r.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PasswordConfirmationMismatchCanBeCorrectedAndResubmitted()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        var token = await GetToken(c, "/setup");
        var bad = await c.PostAsync("/setup", Form(token, "owner", "ValidPass123!", "different"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var fresh = ExtractToken(await bad.Content.ReadAsStringAsync());
        var good = await c.PostAsync("/setup", Form(fresh, "owner", "ValidPass123!", "ValidPass123!"));
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
    }

    [Fact]
    public async Task IdentityPasswordValidationFailureCanBeCorrectedAndResubmitted()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        var token = await GetToken(c, "/setup");
        var bad = await c.PostAsync("/setup", Form(token, "owner", "short", "short"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var fresh = ExtractToken(await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/setup", Form(fresh, "owner", "ValidPass123!", "ValidPass123!"))).StatusCode);
    }

    [Fact]
    public async Task InvalidLoginReturnsGenericFailureWithoutDisclosingUsername()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c);
        await c.PostAsync("/logout", Form(await GetLogoutToken(c)));
        var body = await InvalidLoginBody(c, "missing");
        Assert.Contains("Invalid username or password.", body);
        Assert.DoesNotContain("missing", body);
    }

    [Fact]
    public async Task FailedLoginCanBeCorrectedAndResubmittedWithoutReloading()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c); await c.PostAsync("/logout", Form(await GetLogoutToken(c)));
        var token = await GetToken(c, "/login");
        var bad = await c.PostAsync("/login", Form(token, "owner", "wrong"));
        var fresh = ExtractToken(await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/login", Form(fresh, "owner", "ValidPass123!"))).StatusCode);
    }

    [Fact]
    public async Task ValidLoginSucceedsAndSafeRelativeReturnUrlWorks()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c); await c.PostAsync("/logout", Form(await GetLogoutToken(c)));
        var token = await GetToken(c, "/login?returnUrl=%2Fapi%2Fv1%2Fhealth");
        var r = await c.PostAsync("/login", Form(token, "owner", "ValidPass123!", returnUrl: "/api/v1/health"));
        Assert.Equal("/api/v1/health", r.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ExternalAndProtocolRelativeReturnUrlsAreRejected()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c); await c.PostAsync("/logout", Form(await GetLogoutToken(c)));
        foreach (var url in new[] { "https://example.invalid/", "//example.invalid/" })
        {
            var token = await GetToken(c, "/login?returnUrl=" + Uri.EscapeDataString(url));
            var r = await c.PostAsync("/login", Form(token, "owner", "ValidPass123!", returnUrl: url));
            Assert.Equal("/", r.Headers.Location?.OriginalString);
            await c.PostAsync("/logout", Form(await GetLogoutToken(c)));
        }
    }

    [Fact]
    public async Task LogoutIsPostOnlyAndValidLogoutClearsAuthentication()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await c.GetAsync("/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/logout", Form(await GetLogoutToken(c)))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task DisabledOwnerCannotAuthorize()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c); await c.PostAsync("/logout", Form(await GetLogoutToken(c)));
        await f.DisableOwner();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/login", Form(await GetToken(c, "/login"), "owner", "ValidPass123!"))).StatusCode);
    }

    [Fact]
    public async Task LockoutOccursAccordingToConfiguredPolicy()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c); await c.PostAsync("/logout", Form(await GetLogoutToken(c)));
        for (var i = 0; i < 5; i++) _ = await c.PostAsync("/login", Form(await GetToken(c, "/login"), "owner", "wrong"));
        using var scope = f.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<OwnerUser>>().FindByNameAsync("owner");
        Assert.True(user?.LockoutEnd > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task AuthenticationAuditRecordsDoNotContainSecrets()
    {
        await using var f = new ManagerFactory();
        using var c = f.CreateClient(new() { AllowAutoRedirect = false });
        await SetupOwner(c); await c.PostAsync("/logout", Form(await GetLogoutToken(c))); await InvalidLoginBody(c, "owner");
        using var scope = f.Services.CreateScope();
        var details = string.Join("\n", await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManagerDbContext>>().CreateDbContext().AuditRecords.Select(a => a.Detail).ToListAsync());
        Assert.DoesNotContain("ValidPass123!", details); Assert.DoesNotContain("PasswordHash", details); Assert.DoesNotContain("RequestVerificationToken", details); Assert.DoesNotContain("Cookie", details);
    }

    [Fact]
    public async Task ConcurrentFirstRunSetupAttemptsCreateExactlyOneOwner()
    {
        await using var f = new ManagerFactory();
        var tasks = Enumerable.Range(0, 8).Select(async i => { using var c = f.CreateClient(new() { AllowAutoRedirect = false }); return await SetupOwner(c, "owner" + i); });
        await Task.WhenAll(tasks);
        using var scope = f.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<UserManager<OwnerUser>>().Users.CountAsync());
    }

    [Fact]
    public async Task MilestoneOneDatabaseUpgradePreservesAuditRecordsAndCreatesIdentityTables()
    {
        var root = Path.Combine(Path.GetTempPath(), "psm-m1-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "seed.sql"), "");
            var db = Path.Combine(root, "manager.db");
            await using (var cx = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db}")) { await cx.OpenAsync(); var cmd = cx.CreateCommand(); cmd.CommandText = "CREATE TABLE AuditRecords (Id TEXT NOT NULL PRIMARY KEY, OccurredAtUtc TEXT NOT NULL, Actor TEXT NOT NULL, Action TEXT NOT NULL, Outcome TEXT NOT NULL, Detail TEXT NULL); INSERT INTO AuditRecords VALUES ('11111111-1111-1111-1111-111111111111','2026-01-01T00:00:00Z','tester','m1','success','preserve');"; await cmd.ExecuteNonQueryAsync(); }
            await using var f = new ManagerFactory(root);
            _ = f.Services;
            using var scope = f.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManagerDbContext>>().CreateDbContext();
            Assert.Equal("preserve", (await ctx.AuditRecords.SingleAsync()).Detail);
            Assert.Contains("AspNetUsers", await ctx.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type='table'").ToListAsync());
            _ = f.Services;
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FactoryUsesTemporaryDataStorageAndNotProductionProgramData()
    {
        await using var f = new ManagerFactory();
        Assert.StartsWith(Path.GetTempPath(), f.DataRoot, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(f.DataRoot, "manager.db")));
        Assert.DoesNotContain("ProgramData", f.DataRoot, StringComparison.OrdinalIgnoreCase);
    }

    static async Task AssertAnonymousGet(string path) { await using var f = new ManagerFactory(); using var c = f.CreateClient(new() { AllowAutoRedirect = false }); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(path)).StatusCode); }
    static async Task AssertAnonymousPost(string path) { await using var f = new ManagerFactory(); using var c = f.CreateClient(new() { AllowAutoRedirect = false }); Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsync(path, null)).StatusCode); }
    static async Task<HttpResponseMessage> SetupOwner(HttpClient c, string username = "owner") => await c.PostAsync("/setup", Form(await GetToken(c, "/setup"), username, "ValidPass123!", "ValidPass123!"));
    static async Task<string> InvalidLoginBody(HttpClient c, string username) { var r = await c.PostAsync("/login", Form(await GetToken(c, "/login"), username, "wrong")); return await r.Content.ReadAsStringAsync(); }
    static async Task<string> GetToken(HttpClient c, string path) => ExtractToken(await (await c.GetAsync(path)).Content.ReadAsStringAsync());
    static async Task<string> GetLogoutToken(HttpClient c) => ExtractToken(await (await c.GetAsync("/")).Content.ReadAsStringAsync());
    static string ExtractToken(string html) => Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    static FormUrlEncodedContent Form(string token, string? username = null, string? password = null, string? confirmation = null, string? returnUrl = null)
    {
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };
        if (username is not null) fields["Username"] = username; if (password is not null) fields["Password"] = password; if (confirmation is not null) fields["ConfirmPassword"] = confirmation; if (returnUrl is not null) fields["ReturnUrl"] = returnUrl;
        return new FormUrlEncodedContent(fields);
    }

    private sealed class ManagerFactory : WebApplicationFactory<Program>
    {
        public string DataRoot { get; }
        public ManagerFactory(string? dataRoot = null) => DataRoot = dataRoot ?? Path.Combine(Path.GetTempPath(), "psm-tests-" + Guid.NewGuid());
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(Directory.GetCurrentDirectory()); builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Manager:ListenUrl"] = "http://127.0.0.1:0", ["Manager:DataRoot"] = DataRoot }));
        }
        public async Task DisableOwner() { using var scope = Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<OwnerUser>>(); var user = await users.FindByNameAsync("owner"); Assert.NotNull(user); user.IsEnabled = false; await users.UpdateAsync(user); }
        public override async ValueTask DisposeAsync() { await base.DisposeAsync(); if (Directory.Exists(DataRoot)) Directory.Delete(DataRoot, true); }
    }
}
