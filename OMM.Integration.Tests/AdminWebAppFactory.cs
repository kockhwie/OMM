using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OMM.Integration.Tests;

/// <summary>
/// WebApplicationFactory for OMM.Admin that runs in the "Integration" environment.
/// Database startup checks are bypassed so tests run quickly without live Postgres.
/// </summary>
public class AdminWebAppFactory : WebApplicationFactory<OMM.Admin.Components.App>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Integration");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Port=5432;Database=test;Username=postgres;Password=postgres");
    }
}


