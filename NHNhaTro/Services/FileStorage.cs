namespace NHNhaTro.Services;

/// <summary>
/// Lưu file tải lên (ảnh CCCD, chứng từ, ảnh hiện trạng, ảnh sửa chữa...) vào thư mục Uploads, trả về đường dẫn web "/Uploads/...".
/// File được phục vụ qua endpoint /Uploads có yêu cầu đăng nhập, trừ thư mục "Public" (ảnh quảng cáo).
/// </summary>
public class FileStorage(IConfiguration config, IWebHostEnvironment env)
{
    public const string WebPrefix = "/Uploads";
    public const string PublicFolder = "Public";
    public const long MaxFileSize = 15 * 1024 * 1024;

    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic"];
    public static readonly string[] DocumentExtensions = [.. ImageExtensions, ".pdf"];
    /// <summary>Chat nội bộ: thêm file văn phòng, văn bản, nén.</summary>
    public static readonly string[] ChatExtensions = [.. DocumentExtensions, ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip", ".rar"];

    /// <summary>Thư mục vật lý chứa Uploads. Cấu hình "Storage:UploadsPath", mặc định &lt;ContentRoot&gt;/Uploads.</summary>
    public string RootPath
    {
        get
        {
            var configured = config["Storage:UploadsPath"];
            var path = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "Uploads")
                : Path.GetFullPath(configured, env.ContentRootPath);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>Lưu file vào thư mục con theo loại và tháng, ví dụ /Uploads/Repairs/2026-10/....jpg</summary>
    public async Task<string> SaveAsync(Stream content, string originalName, string category, bool isPublic = false, string[]? allowed = null)
    {
        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        allowed ??= DocumentExtensions;
        if (!allowed.Contains(ext))
            throw new InvalidOperationException("Định dạng file không được hỗ trợ. Chỉ chấp nhận: " + string.Join(", ", allowed));

        var safeCategory = new string(category.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        var relativeDir = Path.Combine(isPublic ? PublicFolder : "", safeCategory, DateTime.Now.ToString("yyyy-MM"));
        var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var dir = Path.Combine(RootPath, relativeDir);
        Directory.CreateDirectory(dir);
        await using (var fs = File.Create(Path.Combine(dir, fileName)))
        {
            await content.CopyToAsync(fs);
        }
        return $"{WebPrefix}/{relativeDir.Replace('\\', '/')}/{fileName}";
    }

    public static bool IsPublicPath(string relativePath) =>
        relativePath.Replace('\\', '/').StartsWith(PublicFolder + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Đổi đường dẫn web "/Uploads/..." thành đường dẫn vật lý, null nếu không hợp lệ.</summary>
    public string? ResolvePhysicalPath(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath)) return null;
        var rel = webPath.Replace('\\', '/');
        if (rel.StartsWith("~")) rel = rel[1..];
        if (!rel.StartsWith(WebPrefix + "/", StringComparison.OrdinalIgnoreCase)) return null;
        rel = rel[(WebPrefix.Length + 1)..];

        var root = Path.GetFullPath(RootPath);
        var full = Path.GetFullPath(Path.Combine(root, rel));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    public void Delete(string? webPath)
    {
        var path = ResolvePhysicalPath(webPath);
        try
        {
            if (path != null && File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
