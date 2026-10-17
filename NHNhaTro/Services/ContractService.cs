using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public class ContractFilter
{
    public string? SearchTerm { get; set; }
    public string? ContractType { get; set; }
    /// <summary>Active, Expired, Ended hoặc NearExpiry (còn ≤ 30 ngày).</summary>
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? CompanyId { get; set; }
    /// <summary>id, id_desc, Date, date_desc, Status, status_desc, EndDate, enddate_desc.</summary>
    public string? SortOrder { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ContractListResult
{
    public List<Contract> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int ExpiredCount { get; init; }
    public int EndedCount { get; init; }
    public int NearExpiryCount { get; init; }
    public decimal TotalActiveValue { get; init; }
    public int TotalRoomsRented { get; init; }
    public int TotalTenants { get; init; }
}

public class RoomChoice
{
    public int RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public string Area { get; set; } = "";
    public int Floor { get; set; }
    public decimal DefaultPrice { get; set; }
    public decimal AgreedPrice { get; set; }
    public bool IsSelected { get; set; }
    public string? Notes { get; set; }
}

public class ContractForm
{
    public int Id { get; set; }
    public string ContractType { get; set; } = Data.Entities.ContractType.Individual;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public int Months { get; set; } = 12;
    /// <summary>Chỉ dùng khi sửa (khi tạo mới, EndDate = StartDate + Months).</summary>
    public DateTime EndDate { get; set; }
    public DateTime MoveInDate { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 10);
    public decimal ElectricityPrice { get; set; } = 3500;
    public decimal WaterPrice { get; set; } = 15000;
    public string? Note { get; set; }
    public string Status { get; set; } = ContractStatus.Active;
    public string? ContractScanFilePath { get; set; }

    // Hợp đồng cá nhân: 1 phòng
    public int? RoomId { get; set; }
    public decimal? RoomPrice { get; set; }
    public string? RoomNotes { get; set; }

    // Hợp đồng công ty: thông tin công ty + nhiều phòng
    public Company Company { get; set; } = new();
    public List<RoomChoice> Rooms { get; set; } = [];

    public bool IsCompany => ContractType == Data.Entities.ContractType.Company;
}

public class ContractService(IDbContextFactory<ApplicationDbContext> dbFactory, FileStorage files)
{
    private const int NearExpiryDays = 30;

    /// <summary>Hợp đồng Active đã quá ngày kết thúc chuyển sang Expired (như bản cũ làm khi mở danh sách).</summary>
    public async Task<int> UpdateExpiredContractsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var today = DateTime.Today;
        return await db.Contracts
            .Where(c => c.Status == ContractStatus.Active && c.EndDate < today)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContractStatus.Expired));
    }

    public static IQueryable<Contract> ApplyFilter(IQueryable<Contract> query, ContractFilter f)
    {
        if (!string.IsNullOrWhiteSpace(f.SearchTerm))
        {
            var term = f.SearchTerm.Trim();
            int.TryParse(term.TrimStart('#'), out var id);
            query = query.Where(c =>
                c.Id == id ||
                c.ContractRooms.Any(cr => cr.Room.Name.Contains(term)) ||
                c.ContractTenants.Any(ct => ct.Tenant.FullName.Contains(term) || ct.Tenant.IdentityCard.Contains(term)) ||
                (c.Company != null && c.Company.CompanyName.Contains(term)));
        }
        if (!string.IsNullOrEmpty(f.ContractType))
            query = query.Where(c => c.ContractType == f.ContractType);
        if (!string.IsNullOrEmpty(f.Status))
        {
            if (f.Status == "NearExpiry")
            {
                var limit = DateTime.Today.AddDays(NearExpiryDays);
                query = query.Where(c => c.Status == ContractStatus.Active && c.EndDate <= limit);
            }
            else
            {
                query = query.Where(c => c.Status == f.Status);
            }
        }
        if (f.FromDate.HasValue) query = query.Where(c => c.StartDate >= f.FromDate.Value);
        if (f.ToDate.HasValue) query = query.Where(c => c.EndDate <= f.ToDate.Value);
        if (f.CompanyId.HasValue) query = query.Where(c => c.CompanyId == f.CompanyId.Value);
        return query;
    }

    private static IQueryable<Contract> ApplySort(IQueryable<Contract> query, string? sort) => sort switch
    {
        "id" => query.OrderBy(c => c.Id),
        "Date" => query.OrderBy(c => c.StartDate),
        "date_desc" => query.OrderByDescending(c => c.StartDate),
        "Status" => query.OrderBy(c => c.Status),
        "status_desc" => query.OrderByDescending(c => c.Status),
        "EndDate" => query.OrderBy(c => c.EndDate),
        "enddate_desc" => query.OrderByDescending(c => c.EndDate),
        _ => query.OrderByDescending(c => c.Id)
    };

    public async Task<ContractListResult> ListAsync(ContractFilter f)
    {
        await UpdateExpiredContractsAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = ApplyFilter(db.Contracts.AsNoTracking(), f);

        var limit = DateTime.Today.AddDays(NearExpiryDays);
        var stats = await query.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            Active = g.Count(c => c.Status == ContractStatus.Active),
            Expired = g.Count(c => c.Status == ContractStatus.Expired),
            Ended = g.Count(c => c.Status == ContractStatus.Ended),
            Near = g.Count(c => c.Status == ContractStatus.Active && c.EndDate <= limit),
            Value = g.Where(c => c.Status == ContractStatus.Active).Sum(c => (decimal?)c.PriceAgreed) ?? 0
        }).FirstOrDefaultAsync();

        var activeIds = query.Where(c => c.Status == ContractStatus.Active).Select(c => c.Id);
        int roomsRented = await db.ContractRooms.Where(cr => activeIds.Contains(cr.ContractId)).Select(cr => cr.RoomId).Distinct().CountAsync();
        int tenants = await db.ContractTenants.Where(ct => activeIds.Contains(ct.ContractId)).Select(ct => ct.TenantId).Distinct().CountAsync();

        int pageSize = Math.Clamp(f.PageSize, 5, 200);
        int page = Math.Max(1, f.Page);
        var items = await ApplySort(query, f.SortOrder)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .Include(c => c.ContractTenants).ThenInclude(ct => ct.Tenant)
            .Include(c => c.Company)
            .AsSplitQuery()
            .ToListAsync();

        return new ContractListResult
        {
            Items = items,
            TotalCount = stats?.Total ?? 0,
            ActiveCount = stats?.Active ?? 0,
            ExpiredCount = stats?.Expired ?? 0,
            EndedCount = stats?.Ended ?? 0,
            NearExpiryCount = stats?.Near ?? 0,
            TotalActiveValue = stats?.Value ?? 0,
            TotalRoomsRented = roomsRented,
            TotalTenants = tenants
        };
    }

    public async Task<List<Contract>> ListForExportAsync(ContractFilter f)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await ApplyFilter(db.Contracts.AsNoTracking(), f)
            .OrderByDescending(c => c.Id)
            .Include(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .Include(c => c.ContractTenants).ThenInclude(ct => ct.Tenant)
            .Include(c => c.Company)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<List<Company>> ListCompaniesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Companies.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();
    }

    public async Task<Company?> FindCompanyByTaxCodeAsync(string taxCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.TaxCode == taxCode.Trim());
    }

    public async Task<Contract?> GetDetailsAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Contracts.AsNoTracking()
            .Include(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .Include(c => c.ContractTenants).ThenInclude(ct => ct.Tenant)
            .Include(c => c.ContractTenants).ThenInclude(ct => ct.Room)
            .Include(c => c.Company)
            .Include(c => c.ContractExtensionHistories)
            .Include(c => c.IncomeExpenses).ThenInclude(ie => ie.Category)
            .Include(c => c.UtilityBills)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>Phòng chưa thuộc hợp đồng đang hiệu lực nào (trừ hợp đồng exceptContractId).</summary>
    public async Task<List<RoomChoice>> GetAvailableRoomsAsync(int? exceptContractId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var busy = db.ContractRooms
            .Where(cr => (cr.Contract.Status == ContractStatus.Active || cr.Contract.Status == ContractStatus.Expired)
                         && cr.ContractId != (exceptContractId ?? 0))
            .Select(cr => cr.RoomId);
        return await db.Rooms.AsNoTracking()
            .Where(r => !busy.Contains(r.Id))
            .OrderBy(r => r.Area).ThenBy(r => r.Floor).ThenBy(r => r.RoomNumber).ThenBy(r => r.Name)
            .Select(r => new RoomChoice { RoomId = r.Id, RoomName = r.Name, Area = r.Area, Floor = r.Floor, DefaultPrice = r.DefaultPrice, AgreedPrice = r.DefaultPrice })
            .ToListAsync();
    }

    public async Task<int> CreateAsync(ContractForm form)
    {
        Validate(form, isCreate: true);
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var contract = new Contract
        {
            ContractType = form.ContractType,
            StartDate = form.StartDate.Date,
            EndDate = form.StartDate.Date.AddMonths(form.Months),
            MoveInDate = form.MoveInDate.Date,
            ElectricityPrice = form.ElectricityPrice,
            WaterPrice = form.WaterPrice,
            Note = form.Note,
            Status = ContractStatus.Active
        };

        var availableIds = (await GetAvailableRoomsAsync()).Select(r => r.RoomId).ToHashSet();

        if (form.IsCompany)
        {
            var company = await UpsertCompanyAsync(db, form.Company);
            contract.Company = company;

            var selected = form.Rooms.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("Vui lòng chọn ít nhất một phòng.");
            foreach (var choice in selected)
            {
                if (!availableIds.Contains(choice.RoomId)) throw new InvalidOperationException($"Phòng {choice.RoomName} đã được thuê.");
                var room = await db.Rooms.FindAsync(choice.RoomId) ?? throw new InvalidOperationException("Không tìm thấy phòng.");
                contract.ContractRooms.Add(new ContractRoom { RoomId = room.Id, PriceAgreed = choice.AgreedPrice, Notes = choice.Notes });
                room.IsOccupied = true;
            }
            contract.PriceAgreed = selected.Sum(r => r.AgreedPrice);
        }
        else
        {
            if (form.RoomId is not int roomId) throw new InvalidOperationException("Vui lòng chọn phòng.");
            var room = await db.Rooms.FindAsync(roomId) ?? throw new InvalidOperationException("Không tìm thấy phòng.");
            if (!availableIds.Contains(roomId)) throw new InvalidOperationException($"Phòng {room.Name} đã được thuê.");
            contract.PriceAgreed = form.RoomPrice ?? room.DefaultPrice;
            contract.ContractRooms.Add(new ContractRoom { RoomId = roomId, PriceAgreed = contract.PriceAgreed, Notes = form.RoomNotes ?? form.Note });
            room.IsOccupied = true;
        }

        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return contract.Id;
    }

    public async Task<ContractForm?> GetEditFormAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.Contracts.AsNoTracking()
            .Include(x => x.ContractRooms).ThenInclude(cr => cr.Room)
            .Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return null;

        var form = new ContractForm
        {
            Id = c.Id,
            ContractType = c.ContractType ?? ContractType.Individual,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Months = Fmt.MonthsBetween(c.StartDate, c.EndDate),
            MoveInDate = c.MoveInDate,
            ElectricityPrice = c.ElectricityPrice,
            WaterPrice = c.WaterPrice,
            Note = c.Note,
            Status = c.Status,
            ContractScanFilePath = c.ContractScanFilePath,
            Company = c.Company ?? new Company()
        };

        var available = await GetAvailableRoomsAsync(id);
        if (form.IsCompany)
        {
            foreach (var r in available)
            {
                var cr = c.ContractRooms.FirstOrDefault(x => x.RoomId == r.RoomId);
                if (cr != null)
                {
                    r.IsSelected = true;
                    r.AgreedPrice = cr.PriceAgreed;
                    r.Notes = cr.Notes;
                }
            }
            form.Rooms = available;
        }
        else
        {
            var cr = c.ContractRooms.FirstOrDefault();
            form.RoomId = cr?.RoomId;
            form.RoomPrice = cr?.PriceAgreed ?? c.PriceAgreed;
            form.RoomNotes = cr?.Notes;
            form.Rooms = available;
        }
        return form;
    }

    public async Task UpdateAsync(ContractForm form, Stream? scan = null, string? scanName = null)
    {
        Validate(form, isCreate: false);
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var contract = await db.Contracts
            .Include(c => c.ContractRooms)
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Id == form.Id) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");

        contract.StartDate = form.StartDate.Date;
        contract.EndDate = form.EndDate.Date;
        contract.MoveInDate = form.MoveInDate.Date;
        contract.ElectricityPrice = form.ElectricityPrice;
        contract.WaterPrice = form.WaterPrice;
        contract.Note = form.Note;
        contract.Status = form.Status;

        if (scan != null && scanName != null)
        {
            var newPath = await files.SaveContractScanAsync(scan, scanName, contract.Id);
            files.Delete(contract.ContractScanFilePath);
            contract.ContractScanFilePath = newPath;
        }

        var available = (await GetAvailableRoomsAsync(contract.Id)).Select(r => r.RoomId).ToHashSet();
        bool roomsShouldBeOccupied = contract.Status != ContractStatus.Ended;

        if (contract.IsCompany)
        {
            if (!string.IsNullOrWhiteSpace(form.Company.CompanyName))
            {
                var company = contract.Company ?? await UpsertCompanyAsync(db, form.Company);
                CopyCompany(form.Company, company);
                contract.Company = company;
            }

            var selected = form.Rooms.Where(r => r.IsSelected).ToDictionary(r => r.RoomId);
            if (selected.Count == 0) throw new InvalidOperationException("Hợp đồng công ty phải có ít nhất một phòng.");

            foreach (var cr in contract.ContractRooms.Where(cr => !selected.ContainsKey(cr.RoomId)).ToList())
                await RemoveRoomFromContractAsync(db, contract, cr);

            foreach (var choice in selected.Values)
            {
                var cr = contract.ContractRooms.FirstOrDefault(x => x.RoomId == choice.RoomId);
                if (cr == null)
                {
                    if (!available.Contains(choice.RoomId)) throw new InvalidOperationException($"Phòng {choice.RoomName} đã được thuê.");
                    cr = new ContractRoom { RoomId = choice.RoomId };
                    contract.ContractRooms.Add(cr);
                    var room = await db.Rooms.FindAsync(choice.RoomId);
                    if (room != null) room.IsOccupied = roomsShouldBeOccupied;
                }
                cr.PriceAgreed = choice.AgreedPrice;
                cr.Notes = choice.Notes;
            }
            contract.PriceAgreed = contract.ContractRooms.Sum(cr => cr.PriceAgreed);
        }
        else if (form.RoomId is int newRoomId)
        {
            var cr = contract.ContractRooms.FirstOrDefault();
            if (cr == null)
            {
                cr = new ContractRoom { RoomId = newRoomId };
                contract.ContractRooms.Add(cr);
            }
            else if (cr.RoomId != newRoomId)
            {
                if (!available.Contains(newRoomId)) throw new InvalidOperationException("Phòng mới đã được thuê.");
                var oldRoom = await db.Rooms.FindAsync(cr.RoomId);
                if (oldRoom != null) oldRoom.IsOccupied = false;
                // Chuyển người thuê và phiếu báo của hợp đồng sang phòng mới
                await db.ContractTenants.Where(ct => ct.ContractId == contract.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(ct => ct.RoomId, newRoomId));
                cr.RoomId = newRoomId;
            }
            var newRoom = await db.Rooms.FindAsync(newRoomId);
            if (newRoom != null && roomsShouldBeOccupied) newRoom.IsOccupied = true;

            cr.PriceAgreed = form.RoomPrice ?? cr.PriceAgreed;
            cr.Notes = form.RoomNotes;
            contract.PriceAgreed = cr.PriceAgreed;
        }

        if (contract.Status == ContractStatus.Ended)
        {
            var roomIds = contract.ContractRooms.Select(cr => cr.RoomId).ToList();
            await db.Rooms.Where(r => roomIds.Contains(r.Id)).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsOccupied, false));
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ExtendAsync(int id, DateTime newEndDate, string? note)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var contract = await db.Contracts.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        if (contract.Status == ContractStatus.Ended) throw new InvalidOperationException("Không thể gia hạn hợp đồng đã kết thúc.");
        if (newEndDate.Date <= contract.EndDate) throw new InvalidOperationException("Ngày gia hạn mới phải sau ngày kết thúc hiện tại.");

        var oldEnd = contract.EndDate;
        db.ContractExtensionHistories.Add(new ContractExtensionHistory
        {
            ContractId = id,
            OldEndDate = oldEnd,
            NewEndDate = newEndDate.Date,
            ExtendMonths = Fmt.MonthsBetween(oldEnd, newEndDate),
            Note = note?.Length > 255 ? note[..255] : note,
            ExtendedAt = DateTime.Now
        });

        contract.EndDate = newEndDate.Date;
        if (contract.Status == ContractStatus.Expired && contract.EndDate >= DateTime.Today)
            contract.Status = ContractStatus.Active;

        var entry = $"\n[{DateTime.Now:dd/MM/yyyy HH:mm}] Gia hạn từ {oldEnd:dd/MM/yyyy} đến {newEndDate:dd/MM/yyyy}";
        if (!string.IsNullOrWhiteSpace(note)) entry += $" - {note.Trim()}";
        var newNote = (contract.Note ?? "") + entry;
        contract.Note = newNote.Length > 500 ? newNote[^500..] : newNote;

        await db.SaveChangesAsync();
    }

    public async Task EndAsync(int id, DateTime? endDate, decimal? refundAmount, string? refundNote, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var contract = await db.Contracts.Include(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .FirstOrDefaultAsync(c => c.Id == id) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        if (contract.Status == ContractStatus.Ended) throw new InvalidOperationException("Hợp đồng này đã được kết thúc trước đó.");

        contract.Status = ContractStatus.Ended;
        contract.EndDate = (endDate ?? DateTime.Today).Date;
        foreach (var cr in contract.ContractRooms) cr.Room.IsOccupied = false;

        if (refundAmount is > 0)
        {
            var category = await BillingService.GetOrCreateSystemCategoryAsync(db, SystemCategories.DepositRefund, CategoryType.Expense);
            db.IncomeExpenses.Add(new IncomeExpense
            {
                CategoryId = category.Id,
                ContractId = id,
                Amount = refundAmount.Value,
                TransactionDate = contract.EndDate,
                Description = string.IsNullOrWhiteSpace(refundNote) ? $"Trả tiền cọc khi kết thúc hợp đồng {id}" : refundNote.Trim(),
                ReferenceNumber = $"REFUND-{id}-{DateTime.Now:yyyyMMddHHmmss}",
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<int> BatchEndAsync(IEnumerable<int> ids)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var idList = ids.ToList();
        var contracts = await db.Contracts.Include(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .Where(c => idList.Contains(c.Id) && c.Status != ContractStatus.Ended).ToListAsync();
        foreach (var c in contracts)
        {
            c.Status = ContractStatus.Ended;
            c.EndDate = DateTime.Today;
            foreach (var cr in c.ContractRooms) cr.Room.IsOccupied = false;
        }
        await db.SaveChangesAsync();
        return contracts.Count;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var contract = await db.Contracts
            .Include(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .FirstOrDefaultAsync(c => c.Id == id) ?? throw new InvalidOperationException("Hợp đồng không tồn tại.");

        if (await db.IncomeExpenses.AnyAsync(ie => ie.ContractId == id))
            throw new InvalidOperationException("Hợp đồng đã có giao dịch thu chi, không thể xóa. Hãy kết thúc hợp đồng thay vì xóa.");

        foreach (var cr in contract.ContractRooms) cr.Room.IsOccupied = false;
        await db.ContractTenants.Where(ct => ct.ContractId == id).ExecuteDeleteAsync();
        await db.UtilityBills.Where(b => b.ContractId == id).ExecuteDeleteAsync();
        await db.ContractExtensionHistories.Where(h => h.ContractId == id).ExecuteDeleteAsync();
        db.ContractRooms.RemoveRange(contract.ContractRooms);
        db.Contracts.Remove(contract);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        files.Delete(contract.ContractScanFilePath);
    }

    private async Task RemoveRoomFromContractAsync(ApplicationDbContext db, Contract contract, ContractRoom cr)
    {
        var tenantsInRoom = await db.ContractTenants.Where(ct => ct.ContractId == contract.Id && ct.RoomId == cr.RoomId).ToListAsync();
        db.ContractTenants.RemoveRange(tenantsInRoom);
        var room = await db.Rooms.FindAsync(cr.RoomId);
        if (room != null) room.IsOccupied = false;
        contract.ContractRooms.Remove(cr);
        db.ContractRooms.Remove(cr);
    }

    private static async Task<Company> UpsertCompanyAsync(ApplicationDbContext db, Company input)
    {
        if (string.IsNullOrWhiteSpace(input.CompanyName)) throw new InvalidOperationException("Vui lòng nhập tên công ty.");
        Company? company = null;
        if (!string.IsNullOrWhiteSpace(input.TaxCode))
            company = await db.Companies.FirstOrDefaultAsync(c => c.TaxCode == input.TaxCode.Trim());
        if (company == null)
        {
            company = new Company { CreatedAt = DateTime.Now, IsActive = true };
            db.Companies.Add(company);
        }
        CopyCompany(input, company);
        return company;
    }

    private static void CopyCompany(Company from, Company to)
    {
        to.CompanyName = from.CompanyName.Trim();
        to.TaxCode = from.TaxCode?.Trim();
        to.Address = from.Address;
        to.Phone = from.Phone;
        to.Email = from.Email;
        to.Representative = from.Representative;
        to.RepresentativePhone = from.RepresentativePhone;
        to.RepresentativeEmail = from.RepresentativeEmail;
        to.BankAccount = from.BankAccount;
        to.BankName = from.BankName;
        to.UpdatedAt = DateTime.Now;
    }

    private static void Validate(ContractForm f, bool isCreate)
    {
        if (f.ContractType is not (ContractType.Individual or ContractType.Company))
            throw new InvalidOperationException("Vui lòng chọn loại khách hàng.");
        if (isCreate && (f.Months < 1 || f.Months > 120)) throw new InvalidOperationException("Số tháng phải từ 1 đến 120.");
        if (!isCreate && f.EndDate <= f.StartDate) throw new InvalidOperationException("Ngày kết thúc phải sau ngày bắt đầu.");
        if (f.ElectricityPrice < 0 || f.WaterPrice < 0) throw new InvalidOperationException("Giá điện, nước không được âm.");
        if (f.Note?.Length > 500) throw new InvalidOperationException("Ghi chú tối đa 500 ký tự.");
    }
}
