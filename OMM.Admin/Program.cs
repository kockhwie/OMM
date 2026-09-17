using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OMM.Admin.Components;
using OMM.Admin.Components.Account;
using OMM.Admin.Services.Admin;
using OMM.Admin.Data;
using OMM.Shared.Database;
using OMM.Shared.Infrastructure;
using OMM.Shared.Infrastructure.Email;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin", "SuperAdmin"))
    .AddPolicy("RequireSuperAdminRole", policy =>
        policy.RequireRole("SuperAdmin"));

builder.Services.AddDatabaseAvailability();
builder.Services.AddDatabaseAlerting(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString ?? string.Empty, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null)));
// Use a factory so Blazor components can create short-lived, non-competing
// DbContext instances per operation (avoids "second operation" concurrency crash).
builder.Services.AddDbContextFactory<MasterDataDbContext>(options =>
    options.UseNpgsql(connectionString ?? string.Empty, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null)));
builder.Services.AddSafeNpgsqlDataSource(connectionString);
builder.Services.AddDatabaseAvailabilityMonitor();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(24));

builder.Services.AddSharedIdentityEmail<ApplicationUser>(builder.Configuration);

// Audit logging service
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// User management service (invite, resend, lockout, deactivate)
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Override Identity cookie defaults so unauthorized requests redirect to our
// custom /login page instead of the scaffolded /Account/Login endpoint.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Run migrations and seed in all environments so the admin schema
// and default accounts are created on Render (Production) on first deploy.
await app.TryInitializeDatabaseAsync<ApplicationDbContext>(
    services => AdminIdentitySeedData.SeedAsync(services, app.Configuration));

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
app.UseDatabaseFailureNotifications("OMM.Admin");
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

