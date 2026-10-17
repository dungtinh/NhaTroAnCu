namespace NHNhaTro.Services;

/// <summary>
/// Lưu file upload (ảnh CCCD, scan hợp đồng) vào thư mục Uploads, giữ dạng đường dẫn "/Uploads/..."
/// giống bản MVC cũ để dữ liệu cũ trong database vẫn dùng được. File được phục vụ qua endpoint /Uploads
/// có yêu cầu đăng nhập (xem Program.cs) vì chứa thông tin cá nhân.
/// </summary>
public class FileStorage(IConfiguration config, IWebHostEnvironment env)
{
    public const string WebPrefix = "/Uploads";
    public const long MaxImageSize = 5 * 1024 * 1024;
    public const long MaxScanSize = 10 * 1024 * 1024;

    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"];
    public static readonly string[] ScanExtensions = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>Thư mục vật lý chứa Uploads. Cấu hình bằng "Storage:UploadsPath", mặc định &lt;ContentRoot&gt;/Uploads.</summary>
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

    public async Task<string> SaveTenantPhotoAsync(Stream content, string originalName, string? identityCard)
    {
        var ext = CheckExtension(originalName, ImageExtensions);
        var yearMonth = DateTime.Now.ToString("yyyy-MM");
        var safeId = string.IsNullOrWhiteSpace(identityCard)
            ? ""
            : new string(identityCard.Where(char.IsLetterOrDigit).ToArray()) + "_";
        var fileName = $"tenant_{safeId}{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
        return await SaveAsync(content, Path.Combine("TenantPhotos", yearMonth), fileName);
    }

    public async Task<string> SaveContractScanAsync(Stream content, string originalName, int contractId)
    {
        var ext = CheckExtension(originalName, ScanExtensions);
        var fileName = $"Contract_{contractId}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
        return await SaveAsync(content, "ContractScans", fileName);
    }

    private async Task<string> SaveAsync(Stream content, string relativeDir, string fileName)
    {
        var dir = Path.Combine(RootPath, relativeDir);
        Directory.CreateDirectory(dir);
        await using (var fs = File.Create(Path.Combine(dir, fileName)))
        {
            await content.CopyToAsync(fs);
        }
        return $"{WebPrefix}/{relativeDir.Replace('\\', '/')}/{fileName}";
    }

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

    private static string CheckExtension(string originalName, string[] allowed)
    {
        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            throw new InvalidOperationException("Định dạng file không được hỗ trợ. Chỉ chấp nhận: " + string.Join(", ", allowed));
        return ext;
    }
}
