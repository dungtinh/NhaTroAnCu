using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using NHNhaTro.Services;

namespace NHNhaTro.Components.Shared;

/// <summary>Lớp cơ sở cho các trang nghiệp vụ: thông báo, hộp xác nhận, lấy người dùng hiện tại, bắt lỗi nghiệp vụ.</summary>
public abstract class NhPage : ComponentBase
{
    [Inject] protected ToastService Toast { get; set; } = null!;
    [Inject] protected IJSRuntime JS { get; set; } = null!;
    [Inject] protected NavigationManager Nav { get; set; } = null!;
    [Inject] protected ILoggerFactory LoggerFactory { get; set; } = null!;
    [CascadingParameter] protected Task<AuthenticationState>? AuthState { get; set; }

    protected bool Busy { get; set; }

    protected async Task<string?> GetUserIdAsync()
    {
        if (AuthState == null) return null;
        var state = await AuthState;
        return state.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>Tên đăng nhập (email) của người dùng hiện tại, dùng ghi "người tạo".</summary>
    protected async Task<string?> GetUserNameAsync()
    {
        if (AuthState == null) return null;
        return (await AuthState).User.Identity?.Name;
    }

    protected async Task<bool> IsAdminAsync()
    {
        if (AuthState == null) return false;
        return (await AuthState).User.IsInRole(NHNhaTro.Data.Entities.Roles.Admin);
    }

    protected ValueTask<bool> ConfirmAsync(string message) => JS.InvokeAsync<bool>("confirm", message);

    /// <summary>Chạy thao tác, hiện thông báo lỗi nghiệp vụ thay vì làm hỏng mạch Blazor.</summary>
    protected async Task<bool> RunAsync(Func<Task> action, string? successMessage = null)
    {
        if (Busy) return false;
        Busy = true;
        try
        {
            await action();
            if (successMessage != null) Toast.Success(successMessage);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            Toast.Error(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger(GetType()).LogError(ex, "Lỗi khi xử lý thao tác");
            Toast.Error("Có lỗi xảy ra: " + (ex.InnerException?.Message ?? ex.Message));
            return false;
        }
        finally
        {
            Busy = false;
        }
    }

    protected void OpenInNewTab(string url) => _ = JS.InvokeVoidAsync("open", url, "_blank");
}
