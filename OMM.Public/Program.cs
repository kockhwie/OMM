using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OMM.Public.Components;
using OMM.Public.Components.Account;
using OMM.Public.Data;
using OMM.Public.Services;
using OMM.Shared.Database;
using OMM.Shared.Infrastructure;
using OMM.Shared.Infrastructure.Email;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IMineService, MockMineService>();
builder.Services.AddMemoryCache();
builder.Services.AddDatabaseAvailability();
builder.Services.AddDatabaseAlerting(builder.Configuration);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthorizationBuilder(); // Non-Admin

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString ?? string.Empty, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null)));
builder.Services.AddSafeNpgsqlDataSource(connectionString);
builder.Services.AddDatabaseAvailabilityMonitor();
builder.Services.AddOptions<StockLookupOptions>()
    .Bind(builder.Configuration.GetSection("StockLookup"))
    .Validate(options => options.CacheDays > 0, "StockLookup:CacheDays must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSharedIdentityEmail<ApplicationUser>(builder.Configuration);
builder.Services.AddSingleton<IKlseStockLookupService, KlseStockLookupService>();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    await app.TryInitializeDatabaseAsync<ApplicationDbContext>(services =>
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var stockDataPath = Path.Combine(
            app.Environment.ContentRootPath,
            "wwwroot",
            "data",
            "klse-stocks.json");

        return StockDataSeeder.SeedAsync(dbContext, stockDataPath);
    });
}
else
{
    await app.TryCheckDatabaseAsync<ApplicationDbContext>();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    //await IdentitySeedData.SeedAsync(scope.ServiceProvider, app.Configuration);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseDatabaseFailureNotifications("OMM.Public");
app.UseDatabaseAvailabilityPage();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDatabaseHealthCheck();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
