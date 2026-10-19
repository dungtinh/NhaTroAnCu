using System.Text;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>
/// Kênh phát tin nhắn thời gian thực trong tiến trình: mọi màn hình đang mở (mỗi người một kết nối SignalR của Blazor)
/// nhận tin ngay khi có người gửi, không cần tải lại. Đăng ký ở Blazor bằng Subscribe, hủy khi rời trang.
/// </summary>
public class ChatHub
{
    private readonly List<Func<ChatMessageView, Task>> handlers = [];
    private readonly object gate = new();

    public IDisposable Subscribe(Func<ChatMessageView, Task> handler)
    {
        lock (gate) handlers.Add(handler);
        return new Unsub(() => { lock (gate) handlers.Remove(handler); });
    }

    public async Task PublishAsync(ChatMessageView m)
    {
        Func<ChatMessageView, Task>[] list;
        lock (gate) list = [.. handlers];
        foreach (var h in list)
        {
            try { await h(m); } catch (Exception) { /* mạch đã đóng */ }
        }
    }

    private sealed class Unsub(Action a) : IDisposable { public void Dispose() => a(); }
}

public record ChatMessageView(int Id, DateTime SentAt, string Sender, string Text, string? RoomCode, bool IsAssistant, List<Attachment> Attachments)
{
    /// <summary>Tên hiển thị: phần trước @ của email.</summary>
    public string DisplayName => IsAssistant ? "Trợ lý AI" : Sender.Split('@')[0];
}

public class ChatService(IDbContextFactory<ApplicationDbContext> dbFactory, ChatHub hub, DocumentScanService ai, RoomService rooms, ILogger<ChatService> logger)
{
    public const string AssistantName = "assistant";
    private static readonly string[] AssistantTriggers = ["@ai", "@trợ lý", "@tro ly", "/ai"];

    public async Task<List<ChatMessageView>> GetRecentAsync(int take = 100, int? beforeId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.ChatMessages.AsNoTracking().Include(m => m.Room).AsQueryable();
        if (beforeId is int b) q = q.Where(m => m.Id < b);
        var list = await q.OrderByDescending(m => m.Id).Take(take).ToListAsync();
        var ids = list.Select(m => m.Id).ToList();
        var atts = await db.Attachments.AsNoTracking().Where(a => a.EntityType == AttachmentOwner.Chat && ids.Contains(a.EntityId)).ToListAsync();
        list.Reverse();
        return list.Select(m => ToView(m, atts.Where(a => a.EntityId == m.Id).ToList())).ToList();
    }

    private static ChatMessageView ToView(ChatMessage m, List<Attachment> atts) =>
        new(m.Id, m.SentAt, m.Sender, m.Text, m.Room?.Code, m.IsAssistant, atts);

    public async Task<ChatMessageView> SendAsync(string sender, string text, int? roomId, IEnumerable<PendingFile> files)
    {
        text = (text ?? "").Trim();
        var pending = files.ToList();
        if (text.Length == 0 && pending.Count == 0) throw new InvalidOperationException("Nhập nội dung hoặc chọn ảnh.");
        if (text.Length > 4000) throw new InvalidOperationException("Tin nhắn quá dài.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var msg = new ChatMessage { SentAt = DateTime.Now, Sender = sender, Text = text.Length == 0 ? "(ảnh)" : text, RoomId = roomId };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();
        AttachmentService.Attach(db, AttachmentOwner.Chat, msg.Id, pending, sender);
        await db.SaveChangesAsync();
        await MarkReadAsync(sender, msg.Id);

        var room = roomId is int rid ? await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid) : null;
        var atts = await db.Attachments.AsNoTracking().Where(a => a.EntityType == AttachmentOwner.Chat && a.EntityId == msg.Id).ToListAsync();
        var view = new ChatMessageView(msg.Id, msg.SentAt, sender, msg.Text, room?.Code, false, atts);
        await hub.PublishAsync(view);

        if (AssistantTriggers.Any(t => text.StartsWith(t, StringComparison.OrdinalIgnoreCase)))
            _ = Task.Run(() => AnswerAsync(text, room?.Code));
        return view;
    }

    /// <summary>Trợ lý AI trả lời trong chat, có kèm tình hình nhà trọ hiện tại để trả lời đúng số liệu.</summary>
    private async Task AnswerAsync(string question, string? roomCode)
    {
        string answer;
        try
        {
            var q = question;
            foreach (var t in AssistantTriggers) if (q.StartsWith(t, StringComparison.OrdinalIgnoreCase)) { q = q[t.Length..].Trim(); break; }
            var context = await BuildContextAsync(roomCode);
            var prompt = $"""
                Bạn là trợ lý của phần mềm quản lý nhà trọ An Cư, trả lời cho nhân viên bằng tiếng Việt, ngắn gọn, đúng số liệu dưới đây.
                Không bịa số. Nếu câu hỏi ngoài dữ liệu thì nói là không có thông tin và gợi ý xem ở trang nào.
                Các trang: Trang chính, Nhận phòng, Trả phòng, Thu tiền tháng, Sửa chữa, Mua sắm, Sơ đồ phòng, Khách thuê, Thu chi, Báo cáo, Cấu hình, Hướng dẫn.

                TÌNH HÌNH HIỆN TẠI ({DateTime.Now:dd/MM/yyyy HH:mm}):
                {context}

                CÂU HỎI: {q}
                """;
            answer = await ai.AskTextAsync(prompt);
            if (string.IsNullOrWhiteSpace(answer)) answer = "Tôi chưa có câu trả lời cho câu này.";
        }
        catch (InvalidOperationException ex) { answer = ex.Message; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Trợ lý AI lỗi");
            answer = "Trợ lý đang gặp lỗi kết nối, thử lại sau.";
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var msg = new ChatMessage { SentAt = DateTime.Now, Sender = AssistantName, Text = answer.Length > 4000 ? answer[..4000] : answer, IsAssistant = true };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();
        await hub.PublishAsync(new ChatMessageView(msg.Id, msg.SentAt, msg.Sender, msg.Text, null, true, []));
    }

    private async Task<string> BuildContextAsync(string? roomCode)
    {
        var map = await rooms.GetMapAsync();
        var sb = new StringBuilder();
        sb.AppendLine($"Tổng {map.Total} phòng, {map.Occupied} có khách, {map.Empty} trống. Kỳ thu hiện tại {map.BillingMonth:00}/{map.BillingYear}. Tổng còn phải thu {Fmt.Vnd(map.Outstanding)}.");
        var debt = map.Rooms.Where(r => r.Outstanding > 0).ToList();
        if (debt.Count > 0) sb.AppendLine("Phòng còn nợ: " + string.Join("; ", debt.Select(r => $"{r.Code} ({r.RepresentativeName}) nợ {Fmt.Vnd(r.Outstanding)}")));
        var nobill = map.Rooms.Where(r => r.Color == RoomColor.NoBill).Select(r => r.Code).ToList();
        if (nobill.Count > 0) sb.AppendLine("Chưa lập phiếu kỳ này: " + string.Join(", ", nobill));
        sb.AppendLine("Phòng có khách: " + string.Join("; ", map.Rooms.Where(r => r.LeaseId != null).Select(r => $"{r.Code}: {r.RepresentativeName}, {r.PersonCount} người, vào {r.StartDate:dd/MM/yyyy}, giá {Fmt.Money(r.Rent)}")));
        sb.AppendLine("Phòng trống: " + string.Join(", ", map.Rooms.Where(r => r.LeaseId == null && r.Status != RoomStatus.Maintenance).Select(r => $"{r.Code} ({Fmt.Money(r.Rent)})")));
        var maint = map.Rooms.Where(r => r.Status == RoomStatus.Maintenance).Select(r => r.Code).ToList();
        if (maint.Count > 0) sb.AppendLine("Đang sửa: " + string.Join(", ", maint));
        if (roomCode != null && await rooms.GetDetailsAsync(roomCode) is { } d)
        {
            sb.AppendLine($"Chi tiết phòng {roomCode}: giá {Fmt.Money(d.Room.Rent)}, đồ đạc: {string.Join(", ", d.Furniture.Where(f => f.Quantity > 0).Select(f => $"{f.Name} x{f.Quantity}"))}.");
            foreach (var b in d.Bills.OrderByDescending(b => b.Id).Take(6))
                sb.AppendLine($"- Phiếu {BillKind.Display(b.Kind)} {b.Month:00}/{b.Year}: tổng {Fmt.Money(b.TotalAmount)}, đã thu {Fmt.Money(d.BillPaid.GetValueOrDefault(b.Id))}");
        }
        return sb.ToString();
    }

    public async Task<int> CountUnreadAsync(string userName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var last = await db.ChatReads.AsNoTracking().Where(r => r.UserName == userName).Select(r => (int?)r.LastReadId).FirstOrDefaultAsync() ?? 0;
        return await db.ChatMessages.CountAsync(m => m.Id > last && m.Sender != userName);
    }

    public async Task MarkReadAsync(string userName, int lastId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var r = await db.ChatReads.FindAsync(userName);
        if (r == null) db.ChatReads.Add(new ChatRead { UserName = userName, LastReadId = lastId, ReadAt = DateTime.Now });
        else if (r.LastReadId < lastId) { r.LastReadId = lastId; r.ReadAt = DateTime.Now; }
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var m = await db.ChatMessages.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy tin nhắn.");
        db.ChatMessages.Remove(m);
        db.Attachments.RemoveRange(db.Attachments.Where(a => a.EntityType == AttachmentOwner.Chat && a.EntityId == id));
        await db.SaveChangesAsync();
    }
}
