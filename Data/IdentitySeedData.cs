using Microsoft.AspNetCore.Identity;

namespace omm.Data;

public static class IdentitySeedData
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureRoleAsync(roleManager, "Admin");
        await EnsureRoleAsync(roleManager, "SuperAdmin");

        await EnsureUserAsync(
            userManager,
            configuration,
            userName: "superadmin",
            email: "kockhwie@msn.com",
            firstName: "Jason",
            lastName: "Goh",
            role: "SuperAdmin",
            passwordKey: "SeedData:SuperAdminInitialPassword");

        await EnsureUserAsync(
            userManager,
            configuration,
            userName: "kockhwie",
            email: "kockhwie@gmail.com",
            firstName: "Kock Hwie",
            lastName: "Goh",
            role: "Admin",
            passwordKey: "SeedData:AdminInitialPassword");
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            EnsureSucceeded(result, $"create role '{roleName}'");
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        string userName,
        string email,
        string firstName,
        string lastName,
        string role,
        string passwordKey)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            var password = configuration[passwordKey];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Missing required user-secret configuration '{passwordKey}'. Refusing to seed user '{userName}'.");
            }

            user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                MustChangePassword = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            EnsureSucceeded(createResult, $"create user '{userName}'");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            EnsureSucceeded(roleResult, $"assign role '{role}' to user '{userName}'");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to {operation}: {errors}");
        }
    }
}
