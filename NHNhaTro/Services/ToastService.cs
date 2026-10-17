namespace NHNhaTro.Services;

public enum ToastKind { Success, Error, Warning, Info }

public record ToastMessage(Guid Id, ToastKind Kind, string Text);

/// <summary>Thông báo nhỏ góc màn hình (thay SweetAlert/TempData của bản cũ). Scoped theo phiên Blazor.</summary>
public class ToastService
{
    private readonly List<ToastMessage> _items = [];
    public IReadOnlyList<ToastMessage> Items => _items;
    public event Action? Changed;

    /// <summary>Thông báo chờ hiển thị sau khi chuyển trang (giống TempData).</summary>
    public ToastMessage? Pending { get; private set; }

    public void Success(string text) => Show(ToastKind.Success, text);
    public void Error(string text) => Show(ToastKind.Error, text);
    public void Warning(string text) => Show(ToastKind.Warning, text);
    public void Info(string text) => Show(ToastKind.Info, text);

    public void Show(ToastKind kind, string text)
    {
        var msg = new ToastMessage(Guid.NewGuid(), kind, text);
        _items.Add(msg);
        Changed?.Invoke();
        _ = RemoveLaterAsync(msg.Id, kind == ToastKind.Error ? 8000 : 4500);
    }

    public void Dismiss(Guid id)
    {
        if (_items.RemoveAll(t => t.Id == id) > 0) Changed?.Invoke();
    }

    private async Task RemoveLaterAsync(Guid id, int ms)
    {
        await Task.Delay(ms);
        Dismiss(id);
    }
}
