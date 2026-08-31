using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using OMM.Admin.Data;

namespace OMM.Admin.Services.Admin
{
    public record InviteDto(string Username, string Email, string DisplayName, string Role);
    public record UserListDto(string Id, string UserName, string Email, string DisplayName, string[] Roles, bool EmailConfirmed, bool MustChangePassword, string? LockoutEnd);

    public interface IUserManagementService
    {
        Task<IReadOnlyList<UserListDto>> ListAsync(string? searchTerm = null);
        Task<IdentityResult> InviteAsync(InviteDto dto);
        Task<IdentityResult> ResendInviteAsync(string userId);
        Task<IdentityResult> ForcePasswordResetAsync(string userId);
        Task<IdentityResult> SetLockoutAsync(string actorUserId, string userId, bool locked);
        Task<IdentityResult> DeactivateAsync(string actorUserId, string userId);
        Task<IdentityResult> ReactivateAsync(string userId);
        Task<IdentityResult> UpdateRoleAsync(string actorUserId, string userId, string role);
    }
}
