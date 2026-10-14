using Microsoft.AspNetCore.Identity;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Api.Services;

/// <summary>
/// The single admin is the account whose email equals the <c>Admin:Email</c> setting. The Admin role is kept in
/// sync with that setting whenever someone signs in or registers, so no one else can end up as admin.
/// </summary>
public class AdminRoleService(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, IConfiguration config)
{
    public async Task<bool> SyncAsync(ApplicationUser user)
    {
        var adminEmail = config["Admin:Email"];
        var shouldBeAdmin = !string.IsNullOrWhiteSpace(adminEmail) &&
                            string.Equals(user.Email, adminEmail, StringComparison.OrdinalIgnoreCase);
        var isAdmin = await users.IsInRoleAsync(user, JwtTokenService.AdminRole);

        if (shouldBeAdmin && !isAdmin)
        {
            if (!await roles.RoleExistsAsync(JwtTokenService.AdminRole))
                await roles.CreateAsync(new IdentityRole(JwtTokenService.AdminRole));
            await users.AddToRoleAsync(user, JwtTokenService.AdminRole);
        }
        else if (!shouldBeAdmin && isAdmin)
        {
            await users.RemoveFromRoleAsync(user, JwtTokenService.AdminRole);
        }
        return shouldBeAdmin;
    }
}
