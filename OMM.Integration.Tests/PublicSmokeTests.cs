using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OMM.Integration.Tests;

public class PublicSmokeTests : IClassFixture<PublicWebAppFactory>
{
    private readonly PublicWebAppFactory _factory;

    public PublicSmokeTests(PublicWebAppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/learn")]
    [InlineData("/health/db")]
    [InlineData("/not-found")]
    public async Task Get_Endpoints_ReturnsSuccessOrRedirect_WithoutUnhandledExceptions(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(url);

        // Verify it does NOT return 500 (Internal Server Error)
        // SSR delegate serialization errors or missing DI registrations produce 500.
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
