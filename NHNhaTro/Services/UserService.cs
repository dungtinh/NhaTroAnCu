using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public record UserRow(string Id, string Email, bool IsAdmin, bool IsLocked);

public class UserService(UserManager<ApplicationUser> users)
{
    /// <summary>Tạo vai trò; nếu chưa ai là Quản trị thì người dùng đang có trở thành Quản trị (lần nâng cấp đầu).</summary>
    public static async Task EnsureRolesAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var r in new[] { Roles.Admin, Roles.Staff })
            if (!await roleManager.RoleExistsAsync(r)) await roleManager.CreateAsync(new IdentityRole(r));
        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count == 0)
            foreach (var u in await userManager.Users.ToListAsync())
                await userManager.AddToRoleAsync(u, Roles.Admin);
    }

    public async Task<List<UserRow>> ListAsync()
    {
        var admins = (await users.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        return (await users.Users.OrderBy(u => u.Email).ToListAsync())
            .Select(u => new UserRow(u.Id, u.Email ?? u.UserName ?? "", admins.Contains(u.Id), u.LockoutEnd > DateTimeOffset.UtcNow)).ToList();
    }

    public async Task CreateAsync(string email, string password, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Nhập email đăng nhập.");
        var user = new ApplicationUser { UserName = email.Trim(), Email = email.Trim(), EmailConfirmed = true };
        Check(await users.CreateAsync(user, password));
        Check(await users.AddToRoleAsync(user, isAdmin ? Roles.Admin : Roles.Staff));
    }

    public async Task ResetPasswordAsync(string userId, string newPassword)
    {
        var u = await users.FindByIdAsync(userId) ?? throw new InvalidOperationException("Không tìm thấy người dùng.");
        Check(await users.RemovePasswordAsync(u));
        Check(await users.AddPasswordAsync(u, newPassword));
    }

    public async Task SetAdminAsync(string userId, bool isAdmin, string currentUserId)
    {
        var u = await users.FindByIdAsync(userId) ?? throw new InvalidOperationException("Không tìm thấy người dùng.");
        if (!isAdmin && userId == currentUserId) throw new InvalidOperationException("Không tự bỏ quyền Quản trị của chính mình.");
        if (isAdmin)
        {
            await users.RemoveFromRoleAsync(u, Roles.Staff);
            if (!await users.IsInRoleAsync(u, Roles.Admin)) Check(await users.AddToRoleAsync(u, Roles.Admin));
        }
        else
        {
            await users.RemoveFromRoleAsync(u, Roles.Admin);
            if (!await users.IsInRoleAsync(u, Roles.Staff)) Check(await users.AddToRoleAsync(u, Roles.Staff));
        }
    }

    public async Task SetLockedAsync(string userId, bool locked, string currentUserId)
    {
        if (userId == currentUserId) throw new InvalidOperationException("Không tự khóa tài khoản của chính mình.");
        var u = await users.FindByIdAsync(userId) ?? throw new InvalidOperationException("Không tìm thấy người dùng.");
        await users.SetLockoutEnabledAsync(u, true);
        Check(await users.SetLockoutEndDateAsync(u, locked ? DateTimeOffset.MaxValue : null));
        await users.UpdateSecurityStampAsync(u);
    }

    private static void Check(IdentityResult r)
    {
        if (!r.Succeeded) throw new InvalidOperationException(string.Join(" ", r.Errors.Select(e => e.Description)));
    }
}
