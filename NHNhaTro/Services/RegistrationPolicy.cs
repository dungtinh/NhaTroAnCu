using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;

namespace NHNhaTro.Services;

/// <summary>
/// Ứng dụng chứa ảnh CCCD và thông tin cá nhân của người thuê nên không mở đăng ký tự do:
/// chỉ cho đăng ký khi database chưa có tài khoản nào (tài khoản đầu tiên), hoặc khi bật "NhaTro:AllowRegistration".
/// </summary>
public class RegistrationPolicy(IConfiguration config, IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<bool> IsOpenAsync()
    {
        if (config.GetValue<bool>("NhaTro:AllowRegistration")) return true;
        await using var db = await dbFactory.CreateDbContextAsync();
        return !await db.Users.AnyAsync();
    }
}
