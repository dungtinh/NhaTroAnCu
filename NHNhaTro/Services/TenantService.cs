using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public class TenantFilter
{
    public string? SearchName { get; set; }
    public string? SearchCard { get; set; }
    public string? SearchRoom { get; set; }
    /// <summary>Trạng thái hợp đồng; để trống = chỉ hợp đồng Active.</summary>
    public string? Status { get; set; }
    public int? CompanyId { get; set; }
    public string? ContractType { get; set; }
}

public record TenantRow(int ContractTenantId, int TenantId, string TenantName, string IdentityCard, string? PhoneNumber, DateTime? BirthDate,
    string? Gender, string? Ethnicity, string? PermanentAddress, string? VehiclePlate, string? Photo, int RoomId, string RoomName,
    int ContractId, string? ContractType, string ContractStatus, DateTime StartDate, DateTime EndDate, DateTime MoveInDate,
    int? CompanyId, string? CompanyName, bool IsPrimary)
{
    public string? FirstPhoto => Photo?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}

public class TenantInput
{
    public int? TenantId { get; set; }
    public string FullName { get; set; } = "";
    public string IdentityCard { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public string? Ethnicity { get; set; }
    public string? VehiclePlate { get; set; }
    /// <summary>Đường dẫn ảnh CCCD (đã lưu).</summary>
    public List<string> Photos { get; set; } = [];

    public bool IsEmpty => string.IsNullOrWhiteSpace(FullName) && string.IsNullOrWhiteSpace(IdentityCard);
}

public class TenantManagerModel
{
    public int ContractId { get; set; }
    public int RoomId { get; set; }
    public string ContractCode { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? ContractType { get; set; }
    public List<TenantInput> Tenants { get; set; } = [];
}

public record CardScanResult(string? Id, string? Name, DateTime? BirthDate, string? Gender, string? Address, string? Ethnicity, string? Nationality);

/// <summary>Phân loại khách nước ngoài theo cột Dân tộc (giữ quy tắc của bản cũ).</summary>
public static class Nationality
{
    private static readonly string[] ForeignKeywords = ["trung quốc", "china", "chinese", "tq", "nước ngoài", "foreign", "lào", "campuchia", "thái lan"];

    public static bool IsForeign(string? ethnicity) =>
        !string.IsNullOrWhiteSpace(ethnicity) && ForeignKeywords.Any(k => ethnicity.ToLower(Fmt.Vi).Contains(k));

    public static string Of(string? ethnicity)
    {
        if (string.IsNullOrWhiteSpace(ethnicity)) return "Trung Quốc";
        var e = ethnicity.ToLower(Fmt.Vi);
        if (e.Contains("trung quốc") || e.Contains("china") || e.Contains("tq")) return "Trung Quốc";
        if (e.Contains("lào")) return "Lào";
        if (e.Contains("campuchia")) return "Campuchia";
        if (e.Contains("thái lan")) return "Thái Lan";
        return ethnicity;
    }
}

public class TenantService(IDbContextFactory<ApplicationDbContext> dbFactory, FileStorage files, IHttpClientFactory httpFactory, ILogger<TenantService> logger)
{
    public async Task<List<TenantRow>> ListAsync(TenantFilter f)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.ContractTenants.AsNoTracking().AsQueryable();
        q = string.IsNullOrEmpty(f.Status)
            ? q.Where(ct => ct.Contract.Status == ContractStatus.Active)
            : q.Where(ct => ct.Contract.Status == f.Status);
        if (!string.IsNullOrEmpty(f.ContractType)) q = q.Where(ct => ct.Contract.ContractType == f.ContractType);
        if (f.CompanyId.HasValue) q = q.Where(ct => ct.Contract.CompanyId == f.CompanyId);
        if (!string.IsNullOrWhiteSpace(f.SearchName)) q = q.Where(ct => ct.Tenant.FullName.Contains(f.SearchName.Trim()));
        if (!string.IsNullOrWhiteSpace(f.SearchCard)) q = q.Where(ct => ct.Tenant.IdentityCard.Contains(f.SearchCard.Trim()));
        if (!string.IsNullOrWhiteSpace(f.SearchRoom)) q = q.Where(ct => ct.Room.Name.Contains(f.SearchRoom.Trim()));

        var rows = await q.OrderBy(ct => ct.Room.Name).ThenBy(ct => ct.CreatedAt).ThenBy(ct => ct.Tenant.FullName)
            .Select(ct => new
            {
                ct.Id, ct.TenantId, ct.Tenant.FullName, ct.Tenant.IdentityCard, ct.Tenant.PhoneNumber, ct.Tenant.BirthDate, ct.Tenant.Gender,
                ct.Tenant.Ethnicity, ct.Tenant.PermanentAddress, ct.Tenant.VehiclePlate, ct.Tenant.Photo, ct.RoomId, RoomName = ct.Room.Name,
                ct.ContractId, ct.Contract.ContractType, ct.Contract.Status, ct.Contract.StartDate, ct.Contract.EndDate, ct.Contract.MoveInDate,
                ct.Contract.CompanyId, CompanyName = ct.Contract.Company != null ? ct.Contract.Company.CompanyName : null,
                FirstInRoom = db.ContractTenants.Where(o => o.ContractId == ct.ContractId && o.RoomId == ct.RoomId)
                                .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id).Select(o => o.Id).FirstOrDefault()
            })
            .ToListAsync();

        return rows.Select(r => new TenantRow(r.Id, r.TenantId, r.FullName, r.IdentityCard, r.PhoneNumber, r.BirthDate, r.Gender, r.Ethnicity,
            r.PermanentAddress, r.VehiclePlate, r.Photo, r.RoomId, r.RoomName, r.ContractId, r.ContractType, r.Status, r.StartDate, r.EndDate,
            r.MoveInDate, r.CompanyId, r.CompanyName, r.FirstInRoom == r.Id)).ToList();
    }

    public async Task<TenantManagerModel?> GetManagerAsync(int contractId, int roomId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var contract = await db.Contracts.AsNoTracking()
            .Include(c => c.Company)
            .Include(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        var cr = contract?.ContractRooms.FirstOrDefault(x => x.RoomId == roomId);
        if (contract == null || cr == null) return null;

        var tenants = await db.ContractTenants.AsNoTracking()
            .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
            .OrderBy(ct => ct.CreatedAt).ThenBy(ct => ct.Id)
            .Select(ct => ct.Tenant)
            .ToListAsync();

        return new TenantManagerModel
        {
            ContractId = contractId,
            RoomId = roomId,
            ContractCode = $"HD-{contract.Id:D6}",
            RoomName = cr.Room.Name,
            CompanyName = contract.Company?.CompanyName ?? "Cá nhân",
            ContractType = contract.ContractType,
            Tenants = tenants.Select(t => new TenantInput
            {
                TenantId = t.Id,
                FullName = t.FullName,
                IdentityCard = t.IdentityCard,
                PhoneNumber = t.PhoneNumber,
                BirthDate = t.BirthDate,
                Gender = t.Gender,
                PermanentAddress = t.PermanentAddress,
                Ethnicity = t.Ethnicity,
                VehiclePlate = t.VehiclePlate,
                Photos = t.PhotoList.ToList()
            }).ToList()
        };
    }

    public async Task<TenantInput?> FindByIdentityCardAsync(string identityCard)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.IdentityCard == identityCard.Trim());
        return t == null ? null : new TenantInput
        {
            TenantId = t.Id, FullName = t.FullName, IdentityCard = t.IdentityCard, PhoneNumber = t.PhoneNumber, BirthDate = t.BirthDate,
            Gender = t.Gender, PermanentAddress = t.PermanentAddress, Ethnicity = t.Ethnicity, VehiclePlate = t.VehiclePlate, Photos = t.PhotoList.ToList()
        };
    }

    /// <summary>
    /// Lưu danh sách người thuê của một phòng trong hợp đồng: người cũ được cập nhật (theo số CCCD),
    /// người mới được thêm, người không còn trong danh sách bị gỡ khỏi phòng (hồ sơ người thuê vẫn giữ).
    /// </summary>
    public async Task SaveManagerAsync(int contractId, int roomId, List<TenantInput> inputs)
    {
        var list = inputs.Where(t => !t.IsEmpty).ToList();
        foreach (var t in list)
        {
            if (string.IsNullOrWhiteSpace(t.FullName) || string.IsNullOrWhiteSpace(t.IdentityCard))
                throw new InvalidOperationException("Mỗi người thuê cần có họ tên và số CCCD/CMND.");
            t.FullName = t.FullName.Trim();
            t.IdentityCard = t.IdentityCard.Trim();
        }
        var dup = list.GroupBy(t => t.IdentityCard).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new InvalidOperationException($"Số CCCD {dup.Key} bị nhập trùng.");

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var contract = await db.Contracts.Include(c => c.ContractRooms).FirstOrDefaultAsync(c => c.Id == contractId)
            ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        if (!contract.ContractRooms.Any(cr => cr.RoomId == roomId)) throw new InvalidOperationException("Phòng không thuộc hợp đồng này.");

        var existingLinks = await db.ContractTenants.Include(ct => ct.Tenant)
            .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId).ToListAsync();

        var keepTenantIds = new HashSet<int>();
        foreach (var input in list)
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.IdentityCard == input.IdentityCard);
            if (tenant == null && input.TenantId is int tid)
                tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tid);
            if (tenant == null)
            {
                tenant = new Tenant();
                db.Tenants.Add(tenant);
            }

            tenant.FullName = input.FullName;
            tenant.IdentityCard = input.IdentityCard;
            tenant.PhoneNumber = input.PhoneNumber;
            tenant.BirthDate = input.BirthDate;
            tenant.Gender = input.Gender;
            tenant.PermanentAddress = input.PermanentAddress;
            tenant.Ethnicity = input.Ethnicity;
            tenant.VehiclePlate = input.VehiclePlate;
            tenant.Photo = input.Photos.Count > 0 ? string.Join(";", input.Photos) : null;
            if (contract.CompanyId.HasValue) tenant.CompanyId = contract.CompanyId;
            await db.SaveChangesAsync();

            keepTenantIds.Add(tenant.Id);
            if (!existingLinks.Any(l => l.TenantId == tenant.Id))
            {
                db.ContractTenants.Add(new ContractTenant { ContractId = contractId, RoomId = roomId, TenantId = tenant.Id, CreatedAt = DateTime.Now });
            }
        }

        db.ContractTenants.RemoveRange(existingLinks.Where(l => !keepTenantIds.Contains(l.TenantId)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task RemoveFromRoomAsync(int contractTenantId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var ct = await db.ContractTenants.FindAsync(contractTenantId) ?? throw new InvalidOperationException("Không tìm thấy người thuê.");
        db.ContractTenants.Remove(ct);
        await db.SaveChangesAsync();
    }

    public void DeletePhotoFile(string path) => files.Delete(path);

    public async Task<bool> IsCardScanConfiguredAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FptReaderApis.AnyAsync(x => x.Status);
    }

    /// <summary>Gọi FPT.AI (https://api.fpt.ai/vision/idr/vnm) để đọc thông tin từ ảnh CCCD.</summary>
    public async Task<CardScanResult> ScanIdentityCardAsync(Stream image, string fileName, string contentType)
    {
        string apiKey;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            apiKey = await db.FptReaderApis.Where(x => x.Status).Select(x => x.ApiKey).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Chưa cấu hình API key FPT AI (bảng FPTReaderAPI).");
        }

        var client = httpFactory.CreateClient("fptai");
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(image);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrEmpty(contentType) ? "image/jpeg" : contentType);
        content.Add(file, "image", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.fpt.ai/vision/idr/vnm") { Content = content };
        request.Headers.Add("api-key", apiKey);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("FPT.AI trả về {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Lỗi từ FPT API: {(int)response.StatusCode}");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        if (root.TryGetProperty("errorCode", out var code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() != 0)
            throw new InvalidOperationException(root.TryGetProperty("errorMessage", out var msg) ? msg.GetString() : "Không thể nhận diện CCCD");
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            throw new InvalidOperationException("Không tìm thấy thông tin CCCD trong ảnh");

        var card = data[0];
        string? S(string name) => card.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s
            && !string.IsNullOrWhiteSpace(s) && s != "N/A" ? s.Trim() : null;

        DateTime? dob = DateTime.TryParseExact(S("dob"), ["dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        var sex = S("sex");
        sex = sex?.ToUpperInvariant() switch { "NAM" or "MALE" => "Nam", "NỮ" or "NU" or "FEMALE" => "Nữ", _ => sex };
        return new CardScanResult(S("id"), S("name"), dob, sex, S("home") ?? S("address"), S("ethnicity"), S("nationality"));
    }
}
