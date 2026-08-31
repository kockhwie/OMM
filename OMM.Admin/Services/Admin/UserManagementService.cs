using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OMM.Admin.Data;

namespace OMM.Admin.Services.Admin
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender<ApplicationUser> _emailSender;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration configuration,
            ILogger<UserManagementService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IReadOnlyList<UserListDto>> ListAsync(string? searchTerm = null)
        {
            var users = _userManager.Users.OrderBy(user => user.UserName).AsEnumerable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                users = users.Where(user =>
                    (user.UserName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user.Email?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user.FirstName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user.LastName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var result = new List<UserListDto>();
            foreach (var user in users)
            {
                result.Add(new UserListDto(
                    user.Id,
                    user.UserName ?? string.Empty,
                    user.Email ?? string.Empty,
                    string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    (await _userManager.GetRolesAsync(user)).ToArray(),
                    user.EmailConfirmed,
                    user.MustChangePassword,
                    user.LockoutEnd?.ToString("u")));
            }

            return result;
        }

        public async Task<IdentityResult> InviteAsync(InviteDto dto)
        {
            // basic uniqueness checks
            if (await _userManager.FindByEmailAsync(dto.Email) != null)
                return IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email already in use." });

            if (await _userManager.FindByNameAsync(dto.Username) != null)
                return IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = "Username already in use." });

            if (!await _roleManager.RoleExistsAsync(dto.Role))
                return IdentityResult.Failed(new IdentityError { Code = "MissingRole", Description = "Role does not exist." });

            var user = new ApplicationUser
            {
                UserName = dto.Username,
                Email = dto.Email,
                FirstName = dto.DisplayName,
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return createResult;

            var addRoleResult = await _userManager.AddToRoleAsync(user, dto.Role);
            if (!addRoleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return addRoleResult;
            }

            // Mark must change password and unconfirmed email
            user.MustChangePassword = true;
            user.EmailConfirmed = false;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return updateResult;
            }

            // Generate password reset token (to be used for account setup)
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = System.Web.HttpUtility.UrlEncode(token);

            var baseUrl = _configuration["AdminBaseUrl"] ?? "https://localhost:5001";
            var activationUrl = $"{baseUrl.TrimEnd('/')}/Account/ResetPassword?email={Uri.EscapeDataString(user.Email)}&code={encoded}";

            try
            {
                await _emailSender.SendPasswordResetLinkAsync(user, user.Email, activationUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invite email to {Email}", user.Email);
                await _userManager.DeleteAsync(user);
                return IdentityResult.Failed(new IdentityError { Code = "EmailDeliveryFailed", Description = "The invitation could not be sent." });
            }

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> ResendInviteAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "User not found." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = System.Web.HttpUtility.UrlEncode(token);
            var baseUrl = _configuration["AdminBaseUrl"] ?? "https://localhost:5001";
            var activationUrl = $"{baseUrl.TrimEnd('/')}/Account/ResetPassword?email={Uri.EscapeDataString(user.Email)}&code={encoded}";
            await _emailSender.SendPasswordResetLinkAsync(user, user.Email, activationUrl);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> ForcePasswordResetAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "User not found." });

            user.MustChangePassword = true;
            await _userManager.UpdateAsync(user);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> SetLockoutAsync(string actorUserId, string userId, bool locked)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "User not found." });

            if (locked && actorUserId == userId)
                return IdentityResult.Failed(new IdentityError { Code = "SelfLockout", Description = "You cannot lock your own active account." });

            if (locked)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
            }

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeactivateAsync(string actorUserId, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "User not found." });

            if (actorUserId == userId)
                return IdentityResult.Failed(new IdentityError { Code = "SelfDeactivation", Description = "You cannot deactivate your own active account." });

            return await SetLockoutAsync(actorUserId, userId, true);
        }

        public async Task<IdentityResult> ReactivateAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "User not found." });

            await _userManager.SetLockoutEndDateAsync(user, null);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateRoleAsync(string actorUserId, string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "User not found." });

            if (!await _roleManager.RoleExistsAsync(role))
                return IdentityResult.Failed(new IdentityError { Code = "MissingRole", Description = "Role does not exist." });

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (actorUserId == userId && currentRoles.Contains("SuperAdmin") && role != "SuperAdmin")
                return IdentityResult.Failed(new IdentityError { Code = "SelfDemotion", Description = "You cannot demote your own active account." });

            if (currentRoles.Contains("SuperAdmin") && role != "SuperAdmin")
            {
                var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
                if (superAdmins.Count(activeUser => activeUser.LockoutEnd is null || activeUser.LockoutEnd <= DateTimeOffset.UtcNow) <= 1)
                    return IdentityResult.Failed(new IdentityError { Code = "LastSuperAdmin", Description = "At least one active SuperAdmin must remain." });
            }

            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return removeResult;

            return await _userManager.AddToRoleAsync(user, role);
        }
    }
}
