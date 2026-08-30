using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OMM.Admin.Data;

public static class AdminIdentitySeedData
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Clean up duplicate email accounts if any were created
        var duplicateEmails = await dbContext.Users
            .GroupBy(u => u.NormalizedEmail)
            .Where(g => g.Key != null && g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        foreach (var normalizedEmail in duplicateEmails)
        {
            var duplicates = await dbContext.Users
                .Where(u => u.NormalizedEmail == normalizedEmail)
                .OrderBy(u => u.Id)
                .ToListAsync();

            var toKeep = duplicates.FirstOrDefault(u => u.UserName == "superadmin" || u.UserName == "kockhwie") ?? duplicates.First();
            var toRemove = duplicates.Where(u => u.Id != toKeep.Id).ToList();

            foreach (var user in toRemove)
            {
                await userManager.DeleteAsync(user);
            }
        }

        string[] roles = ["SuperAdmin", "Admin"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create role '{role}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        // 1. Seed SuperAdmin user
        var superAdminUser = await userManager.FindByNameAsync("superadmin");
        if (superAdminUser is null)
        {
            var initialPassword = configuration["SeedData:AdminApp:SuperAdminInitialPassword"];
            if (string.IsNullOrWhiteSpace(initialPassword))
            {
                throw new InvalidOperationException("Configuration 'SeedData:AdminApp:SuperAdminInitialPassword' is missing. Seeding cannot proceed.");
            }

            superAdminUser = new ApplicationUser
            {
                UserName = "superadmin",
                Email = "kockhwie@msn.com",
                FirstName = "Jason",
                LastName = "Goh",
                EmailConfirmed = true,
                MustChangePassword = true
            };

            var createResult = await userManager.CreateAsync(superAdminUser, initialPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create SuperAdmin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(superAdminUser, "SuperAdmin"))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin");
            if (!addToRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign SuperAdmin role: {string.Join(", ", addToRoleResult.Errors.Select(e => e.Description))}");
            }
        }

        // 2. Seed Admin user
        var adminUser = await userManager.FindByNameAsync("kockhwie");
        if (adminUser is null)
        {
            var initialPassword = configuration["SeedData:AdminApp:AdminInitialPassword"];
            if (string.IsNullOrWhiteSpace(initialPassword))
            {
                throw new InvalidOperationException("Configuration 'SeedData:AdminApp:AdminInitialPassword' is missing. Seeding cannot proceed.");
            }

            adminUser = new ApplicationUser
            {
                UserName = "kockhwie",
                Email = "kockhwie@gmail.com",
                FirstName = "Kock Hwie",
                LastName = "Goh",
                EmailConfirmed = true,
                MustChangePassword = true
            };

            var createResult = await userManager.CreateAsync(adminUser, initialPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create Admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
            if (!addToRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign Admin role: {string.Join(", ", addToRoleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
