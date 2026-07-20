using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PalworldServerManager.Web.Tests;

public sealed class AuthenticationFlowTests
{
    [Fact]
    public async Task HealthEndpointRemainsAnonymous()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/server/status")]
    [InlineData("/api/v1/server/logs")]
    public async Task AnonymousGetManagementApisReturnUnauthorized(string path)
    {
        await using var factory = new ManagerFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousStartApiReturnsUnauthorized()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.PostAsync("/api/v1/server/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class ManagerFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(Directory.GetCurrentDirectory());
            builder.UseEnvironment("Development");
            builder.UseSetting("Manager:ListenUrl", "http://127.0.0.1:0");
        }
    }
}
