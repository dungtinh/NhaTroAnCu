using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>File đã tải lên nhưng chưa gắn với đối tượng (chờ lưu form).</summary>
public record PendingFile(string Path, string FileName, string Kind)
{
    public bool IsImage => System.IO.Path.GetExtension(Path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp";
}

public class AttachmentService(IDbContextFactory<ApplicationDbContext> dbFactory, FileStorage files)
{
    public async Task<PendingFile> UploadAsync(Stream content, string fileName, string kind, bool isPublic = false, string[]? allowed = null)
    {
        var path = await files.SaveAsync(content, fileName, kind, isPublic, allowed);
        return new PendingFile(path, fileName, kind);
    }

    /// <summary>Gắn các file đã tải lên vào đối tượng (dùng trong cùng DbContext của nghiệp vụ).</summary>
    public static void Attach(ApplicationDbContext db, string entityType, int entityId, IEnumerable<PendingFile> pending, string? user)
    {
        int order = 0;
        foreach (var p in pending)
            db.Attachments.Add(new Attachment
            {
                EntityType = entityType, EntityId = entityId, Kind = p.Kind, Path = p.Path, FileName = p.FileName,
                SortOrder = order++, CreatedAt = DateTime.Now, CreatedBy = user
            });
    }

    public async Task AddAsync(string entityType, int entityId, IEnumerable<PendingFile> pending, string? user)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        Attach(db, entityType, entityId, pending, user);
        await db.SaveChangesAsync();
    }

    public async Task<List<Attachment>> ListAsync(string entityType, int entityId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Attachments.AsNoTracking().Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.Kind).ThenBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync();
    }

    public async Task<Dictionary<int, List<Attachment>>> ListManyAsync(string entityType, IEnumerable<int> ids)
    {
        var idList = ids.Distinct().ToList();
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.Attachments.AsNoTracking().Where(a => a.EntityType == entityType && idList.Contains(a.EntityId)).OrderBy(a => a.Id).ToListAsync();
        return all.GroupBy(a => a.EntityId).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task DeleteAsync(int attachmentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var a = await db.Attachments.FindAsync(attachmentId) ?? throw new InvalidOperationException("Không tìm thấy file.");
        db.Attachments.Remove(a);
        await db.SaveChangesAsync();
        files.Delete(a.Path);
    }

    /// <summary>Xóa file vật lý của các file tải lên nhưng bỏ không lưu.</summary>
    public void Discard(IEnumerable<PendingFile> pending)
    {
        foreach (var p in pending) files.Delete(p.Path);
    }
}
