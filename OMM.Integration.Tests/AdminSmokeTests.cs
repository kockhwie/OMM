using System.Net;

namespace OMM.Integration.Tests;

public class AdminSmokeTests : IClassFixture<AdminWebAppFactory>
{
    private readonly AdminWebAppFactory _factory;

    public AdminSmokeTests(AdminWebAppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/health/db")]
    [InlineData("/not-found")]
    [InlineData("/Account/AccessDenied")]
    [InlineData("/admin/users")]
    [InlineData("/admin/audit-logs")]
    public async Task Get_Endpoints_ReturnsSuccessOrRedirect_WithoutUnhandledExceptions(string url)
    {
        // Arrange
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync(url);

        // Assert - verify it does NOT return 500 (Internal Server Error)
        // SSR delegate serialization errors or missing DI registrations produce 500.
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
